using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Spice.Data;
using Spice.Models;
using Spice.Models.ViewModels;
using Spice.Utility;
using Stripe;

namespace Spice.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _db;
        public CartController(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public OrderDetailsCart detailsCart { get; set; }

        public async Task<IActionResult> Index()
        {
            detailsCart = new OrderDetailsCart()
            {
                OrderHeader = new Models.OrderHeader()
            };

            detailsCart.OrderHeader.OrderTotal = 0;

            var claimsIdentity = (ClaimsIdentity) User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            var cart = _db.ShoppingCart.Where(c => c.ApplicationUserId == claim.Value);
            if (cart != null)
            {
                detailsCart.ListCart = cart.ToList();
            }
            foreach (var list in detailsCart.ListCart)
            {
                list.MenuItem = await _db.MenuItem.FirstOrDefaultAsync(m => m.Id == list.MenuItemId);
                detailsCart.OrderHeader.OrderTotal = detailsCart.OrderHeader.OrderTotal + (list.MenuItem.Price * list.Count);
                if (list.MenuItem.Description != null)
                {
                    list.MenuItem.Description = SD.ConvertToRawHtml(list.MenuItem.Description);
                    if (list.MenuItem.Description.Length > 100)
                    {
                        list.MenuItem.Description = list.MenuItem.Description.Substring(0, 99) + "...";
                    }
                }

            }

            detailsCart.OrderHeader.OrderTotalOriginal = detailsCart.OrderHeader.OrderTotal;

            if (HttpContext.Session.GetString(SD.sessionCouponCode) != null)
            {
                detailsCart.OrderHeader.CouponCode = HttpContext.Session.GetString(SD.sessionCouponCode);
                var couponFromDb = await _db.Coupon.Where(c => c.Name.ToLower() == detailsCart.OrderHeader.CouponCode.ToLower()).FirstOrDefaultAsync();
                detailsCart.OrderHeader.OrderTotal = SD.DiscountedPrice(couponFromDb, detailsCart.OrderHeader.OrderTotalOriginal);
            }

            return View(detailsCart);
        }

        public async Task<IActionResult> Summary()
        {
            detailsCart = new OrderDetailsCart()
            {
                OrderHeader = new Models.OrderHeader()
            };

            detailsCart.OrderHeader.OrderTotal = 0;

            var claimsIdentity = (ClaimsIdentity) User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            ApplicationUser applicationUser = await _db.ApplicationUser.Where(c => c.Id == claim.Value).FirstOrDefaultAsync();

            var cart = _db.ShoppingCart.Where(c => c.ApplicationUserId == claim.Value);
            if (cart != null)
            {
                detailsCart.ListCart = cart.ToList();
            }
            foreach (var list in detailsCart.ListCart)
            {
                list.MenuItem = await _db.MenuItem.FirstOrDefaultAsync(m => m.Id == list.MenuItemId);
                detailsCart.OrderHeader.OrderTotal = detailsCart.OrderHeader.OrderTotal + (list.MenuItem.Price * list.Count);
            }

            detailsCart.OrderHeader.OrderTotalOriginal = detailsCart.OrderHeader.OrderTotal;
            detailsCart.OrderHeader.PickupName = applicationUser.Name;
            detailsCart.OrderHeader.PickupPhoneNumber = applicationUser.PhoneNumber;
            detailsCart.OrderHeader.PickupTime = DateTime.Now;

            if (HttpContext.Session.GetString(SD.sessionCouponCode) != null)
            {
                detailsCart.OrderHeader.CouponCode = HttpContext.Session.GetString(SD.sessionCouponCode);
                var couponFromDb = await _db.Coupon.Where(c => c.Name.ToLower() == detailsCart.OrderHeader.CouponCode.ToLower()).FirstOrDefaultAsync();
                detailsCart.OrderHeader.OrderTotal = SD.DiscountedPrice(couponFromDb, detailsCart.OrderHeader.OrderTotalOriginal);
            }

            return View(detailsCart);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionNameAttribute("Summary")]
        public async Task<IActionResult> SummaryPost(string StripeToken)
        {
            //OrderDetailsCart object is al gebind aan alle methods binnnen deze controller
            var claimsIdentity = (ClaimsIdentity) User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            detailsCart.ListCart = await _db.ShoppingCart.Where(c => c.ApplicationUserId == claim.Value).ToListAsync();
            detailsCart.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
            detailsCart.OrderHeader.OrderDate = DateTime.Now;
            detailsCart.OrderHeader.UserId = claim.Value;
            detailsCart.OrderHeader.Status = SD.PaymentStatusPending;
            // in Model word pickupdate niet gemapped naar db, daarom word ie hier toegevoegd aan pickup time
            detailsCart.OrderHeader.PickupTime = Convert.ToDateTime(detailsCart.OrderHeader.PickupDate.ToShortDateString() + " " + detailsCart.OrderHeader.PickupTime.ToShortTimeString());

            List<OrderDetails> orderDetailsList = new List<OrderDetails>();
            // Orderheader word hier al aangemaakt omdat we daar straks het id van nodig hebben
            _db.OrderHeader.Add(detailsCart.OrderHeader);
            await _db.SaveChangesAsync();

            detailsCart.OrderHeader.OrderTotal = 0;

            foreach (var item in detailsCart.ListCart)
            {
                item.MenuItem = await _db.MenuItem.FirstOrDefaultAsync(m => m.Id == item.MenuItemId);
                OrderDetails orderDetails = new OrderDetails
                {
                    MenuItemId = item.MenuItemId,
                    OrderId = detailsCart.OrderHeader.Id,
                    Description = item.MenuItem.Description,
                    Name = item.MenuItem.Name,
                    Price = item.MenuItem.Price,
                    Count = item.Count
                };
                detailsCart.OrderHeader.OrderTotalOriginal += orderDetails.Count * orderDetails.Price;
                _db.OrderDetails.Add(orderDetails);
            }

            if (HttpContext.Session.GetString(SD.sessionCouponCode) != null)
            {
                detailsCart.OrderHeader.CouponCode = HttpContext.Session.GetString(SD.sessionCouponCode);
                var couponFromDb = await _db.Coupon.Where(c => c.Name.ToLower() == detailsCart.OrderHeader.CouponCode.ToLower()).FirstOrDefaultAsync();
                detailsCart.OrderHeader.OrderTotal = SD.DiscountedPrice(couponFromDb, detailsCart.OrderHeader.OrderTotalOriginal);
            }
            else
            {
                detailsCart.OrderHeader.OrderTotal = detailsCart.OrderHeader.OrderTotalOriginal;
            }

            detailsCart.OrderHeader.CouponCodeDiscount = detailsCart.OrderHeader.OrderTotalOriginal - detailsCart.OrderHeader.OrderTotal;
            _db.ShoppingCart.RemoveRange(detailsCart.ListCart);
            HttpContext.Session.SetInt32(SD.sessionShoppingCartCount, 0);
            await _db.SaveChangesAsync();
            // Stripe transactie parameters
            var options = new ChargeCreateOptions
            {
                // amount x 100 omdat bedrag van stripe in centen moet.
                Amount = Convert.ToInt32(detailsCart.OrderHeader.OrderTotal * 100),
                Currency = "usd",
                Description = "Order ID: " + detailsCart.OrderHeader.Id,
                Source = StripeToken
            };
            // Stripe charge object aanmaken
            var service = new ChargeService();
            // Stripe transactie aanmaken
            Charge charge = service.Create(options);
            // Indien er geen transactie id in het charge object zit dan is de betaling geweigerd
            if (charge.BalanceTransactionId == null)
            {
                //Transactie geweigerd
                detailsCart.OrderHeader.PaymentStatus = SD.PaymentStatusRejected;
            }
            else
            {
                // transactie id toevoegen aan orderheader
                detailsCart.OrderHeader.TransactionId = charge.BalanceTransactionId;
            }

            if (charge.Status.ToLower() == "succeeded")
            {
                //indien de betaling gelukt is de orderheader aanpassen
                detailsCart.OrderHeader.PaymentStatus = SD.PaymentStatusApproved;
                detailsCart.OrderHeader.Status = SD.StatusSubmitted;
            }
            else
            {
                detailsCart.OrderHeader.PaymentStatus = SD.PaymentStatusRejected;
            }
            await _db.SaveChangesAsync();
            return RedirectToAction("Index", "Home");
            // return RedirectToAction("Confirm", "Order", new { id = detailsCart.OrderHeader.Id });
        }

        public IActionResult AddCoupon()
        {
            if (detailsCart.OrderHeader.CouponCode == null)
            {
                detailsCart.OrderHeader.CouponCode = "";
            }
            HttpContext.Session.SetString(SD.sessionCouponCode, detailsCart.OrderHeader.CouponCode);

            return RedirectToAction(nameof(Index));
        }
        public IActionResult RemoveCoupon()
        {

            HttpContext.Session.SetString(SD.sessionCouponCode, string.Empty);

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Plus(int cartId)
        {
            var cart = await _db.ShoppingCart.FirstOrDefaultAsync(c => c.Id == cartId);
            cart.Count += 1;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> Minus(int cartId)
        {
            var cart = await _db.ShoppingCart.FirstOrDefaultAsync(c => c.Id == cartId);

            if (cart.Count == 1)
            {
                // 1 item in cart so remove item from cart
                _db.ShoppingCart.Remove(cart);
                await _db.SaveChangesAsync();

                var cnt = _db.ShoppingCart.Where(u => u.ApplicationUserId == cart.ApplicationUserId).ToList().Count;
                HttpContext.Session.SetInt32(SD.sessionShoppingCartCount, cnt);
            }
            else
            {
                cart.Count -= 1;
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> Remove(int cartId)
        {
            var cart = await _db.ShoppingCart.FirstOrDefaultAsync(c => c.Id == cartId);
            _db.ShoppingCart.Remove(cart);
            await _db.SaveChangesAsync();

            var cnt = _db.ShoppingCart.Where(u => u.ApplicationUserId == cart.ApplicationUserId).ToList().Count;
            HttpContext.Session.SetInt32(SD.sessionShoppingCartCount, cnt);
            return RedirectToAction(nameof(Index));
        }
    }
}