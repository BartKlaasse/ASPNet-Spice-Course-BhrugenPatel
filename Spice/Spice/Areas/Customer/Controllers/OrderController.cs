using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Spice.Data;
using Spice.Models;
using Spice.Models.ViewModels;
using Spice.Utility;

namespace Spice.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _db;
        // Per pagination pagina worden X rijen getoond afhankelijk van waarde van pagesize
        private int PageSize = 2;
        private readonly IEmailSender _emailSender;

        public OrderController(ApplicationDbContext db, IEmailSender emailSender)
        {
            _db = db;
            _emailSender = emailSender;
        }
        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Confirm(int id)
        {
            var claimsIdentity = (ClaimsIdentity) User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            OrderDetailsViewModel OrderDetailsVM = new OrderDetailsViewModel()
            {
                OrderHeader = await _db.OrderHeader.Include(o => o.ApplicationUser).FirstOrDefaultAsync(o => o.Id == id && o.UserId == claim.Value),
                OrderDetails = await _db.OrderDetails.Where(o => o.OrderId == id).ToListAsync()
            };
            return View(OrderDetailsVM);
        }

        [Authorize]
        public async Task<IActionResult> OrderHistory(int productPage = 1)
        {
            var claimsIdentity = (ClaimsIdentity) User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            OrderListViewModel orderListVM = new OrderListViewModel()
            {
                Orders = new List<OrderDetailsViewModel>()
            };

            List<OrderHeader> orderHeaderList = await _db.OrderHeader.Include(o => o.ApplicationUser).Where(o => o.UserId == claim.Value).ToListAsync();
            foreach (var item in orderHeaderList)
            {
                OrderDetailsViewModel individual = new OrderDetailsViewModel
                {
                    OrderHeader = item,
                    OrderDetails = await _db.OrderDetails.Where(o => o.OrderId == item.Id).ToListAsync()
                };
                orderListVM.Orders.Add(individual);
            }
            var count = orderListVM.Orders.Count;
            orderListVM.Orders = orderListVM.Orders.OrderByDescending(p => p.OrderHeader.Id).Skip((productPage - 1) * PageSize).Take(PageSize).ToList();
            orderListVM.PagingInfo = new PagingInfo()
            {
                CurrentPage = productPage,
                ItemsPerPage = PageSize,
                TotalItem = count,
                urlParam = "/Customer/Order/OrderHistory/?productPage=:"
            };

            return View(orderListVM);
        }

        [Authorize(Roles = SD.KitchenUser + "," + SD.ManagerUser)]
        public async Task<IActionResult> ManageOrder()
        {
            List<OrderDetailsViewModel> orderDetailsVM = new List<OrderDetailsViewModel>();

            List<OrderHeader> orderHeaderList = await _db.OrderHeader.Where(o => o.Status == SD.StatusSubmitted || o.Status == SD.StatusInProcess).OrderByDescending(o => o.PickupTime).ToListAsync();
            foreach (var item in orderHeaderList)
            {
                OrderDetailsViewModel individual = new OrderDetailsViewModel
                {
                    OrderHeader = item,
                    OrderDetails = await _db.OrderDetails.Where(o => o.OrderId == item.Id).ToListAsync()
                };
                orderDetailsVM.Add(individual);
            }

            return View(orderDetailsVM.OrderBy(o => o.OrderHeader.PickupTime).ToList());
        }

        public async Task<IActionResult> GetOrderDetails(int id)
        {
            OrderDetailsViewModel orderDetailsVM = new OrderDetailsViewModel()
            {
                OrderHeader = await _db.OrderHeader.FirstOrDefaultAsync(m => m.Id == id),
                OrderDetails = await _db.OrderDetails.Where(m => m.OrderId == id).ToListAsync()
            };
            orderDetailsVM.OrderHeader.ApplicationUser = await _db.ApplicationUser.FirstOrDefaultAsync(u => u.Id == orderDetailsVM.OrderHeader.UserId);

            return PartialView("_IndividualOrderDetals", orderDetailsVM);
        }
        public async Task<IActionResult> GetOrderStatus(int id)
        {
            OrderDetailsViewModel orderDetailsVM = new OrderDetailsViewModel()
            {
                OrderHeader = await _db.OrderHeader.FirstOrDefaultAsync(m => m.Id == id),
                OrderDetails = await _db.OrderDetails.Where(m => m.OrderId == id).ToListAsync()
            };
            orderDetailsVM.OrderHeader.ApplicationUser = await _db.ApplicationUser.FirstOrDefaultAsync(u => u.Id == orderDetailsVM.OrderHeader.UserId);

            return PartialView("_OrderStatus", orderDetailsVM);
        }

        [Authorize(Roles = SD.ManagerUser + "," + SD.KitchenUser)]
        public async Task<IActionResult> OrderPrepare(int OrderId)
        {
            OrderHeader orderHeader = await _db.OrderHeader.FindAsync(OrderId);
            orderHeader.Status = SD.StatusInProcess;
            await _db.SaveChangesAsync();
            return RedirectToAction("ManageOrder", "Order");
        }

        [Authorize(Roles = SD.ManagerUser + "," + SD.KitchenUser)]
        public async Task<IActionResult> OrderReady(int OrderId)
        {
            OrderHeader orderHeader = await _db.OrderHeader.FindAsync(OrderId);
            orderHeader.Status = SD.StatusReady;
            await _db.SaveChangesAsync();
            await _emailSender.SendEmailAsync(_db.Users.Where(u => u.Id == orderHeader.UserId).FirstOrDefault().Email, "Spice Order is ready for pickup " + orderHeader.Id.ToString(), "order is ready for pickup");
            return RedirectToAction("ManageOrder", "Order");
        }

        [Authorize(Roles = SD.ManagerUser + "," + SD.KitchenUser)]
        public async Task<IActionResult> OrderCancel(int OrderId)
        {
            OrderHeader orderHeader = await _db.OrderHeader.FindAsync(OrderId);
            orderHeader.Status = SD.StatusCancelled;
            await _db.SaveChangesAsync();
            await _emailSender.SendEmailAsync(_db.Users.Where(u => u.Id == orderHeader.UserId).FirstOrDefault().Email, "Spice Order Cancelled " + orderHeader.Id.ToString(), "order has been cancelled");
            return RedirectToAction("ManageOrder", "Order");
        }

        [Authorize]
        public async Task<IActionResult> OrderPickup(int productPage = 1, string searchEmail = null, string searchName = null, string searchPhone = null)
        {
            // var claimsIdentity = (ClaimsIdentity) User.Identity;
            // var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            OrderListViewModel orderListVM = new OrderListViewModel()
            {
                Orders = new List<OrderDetailsViewModel>()
            };
            StringBuilder param = new StringBuilder();
            param.Append("/Customer/Order/OrderPickup/?productPage=:");
            param.Append("&searchName=");
            if (searchName != null)
            {
                param.Append(searchName);
            }
            param.Append("&searchEmail=");
            if (searchEmail != null)
            {
                param.Append(searchEmail);
            }
            param.Append("&searchPhone=");
            if (searchPhone != null)
            {
                param.Append(searchPhone);
            }
            List<OrderHeader> orderHeaderList = new List<OrderHeader>();
            if (searchName != null || searchEmail != null || searchPhone != null)
            {
                var User = new ApplicationUser();
                orderHeaderList = new List<OrderHeader>();

                if (searchName != null)
                {
                    orderHeaderList = await _db.OrderHeader.Include(o => o.ApplicationUser).Where(u => u.PickupName.ToLower().Contains(searchName.ToLower())).OrderByDescending(o => o.OrderDate).ToListAsync();
                }
                else
                {
                    if (searchEmail != null)
                    {
                        User = await _db.ApplicationUser.Where(u => u.Email.ToLower().Contains(searchEmail.ToLower())).FirstOrDefaultAsync();
                        orderHeaderList = await _db.OrderHeader.Include(o => o.ApplicationUser).Where(o => o.UserId == User.Id).OrderByDescending(o => o.OrderDate).ToListAsync();
                    }
                    else
                    {
                        if (searchPhone != null)
                        {
                            orderHeaderList = await _db.OrderHeader.Include(o => o.ApplicationUser).Where(u => u.PickupPhoneNumber.Contains(searchPhone)).OrderByDescending(o => o.OrderDate).ToListAsync();
                        }
                    }
                }
            }
            else
            {

                orderHeaderList = await _db.OrderHeader.Include(o => o.ApplicationUser).Where(o => o.Status == SD.StatusReady).ToListAsync();
            }
            foreach (var item in orderHeaderList)
            {
                OrderDetailsViewModel individual = new OrderDetailsViewModel
                {
                    OrderHeader = item,
                    OrderDetails = await _db.OrderDetails.Where(o => o.OrderId == item.Id).ToListAsync()
                };
                orderListVM.Orders.Add(individual);
            }

            var count = orderListVM.Orders.Count;
            orderListVM.Orders = orderListVM.Orders.OrderByDescending(p => p.OrderHeader.Id).Skip((productPage - 1) * PageSize).Take(PageSize).ToList();
            orderListVM.PagingInfo = new PagingInfo()
            {
                CurrentPage = productPage,
                ItemsPerPage = PageSize,
                TotalItem = count,
                urlParam = param.ToString()
            };

            return View(orderListVM);
        }

        [Authorize(Roles = SD.ManagerUser + "," + SD.FrontDeskUser)]
        [HttpPost]
        [ActionName("OrderPickup")]
        public async Task<IActionResult> OrderPickupPost(int OrderId)
        {
            OrderHeader orderHeader = await _db.OrderHeader.FindAsync(OrderId);
            orderHeader.Status = SD.StatusCompleted;
            await _db.SaveChangesAsync();
            await _emailSender.SendEmailAsync(_db.Users.Where(u => u.Id == orderHeader.UserId).FirstOrDefault().Email, "Spice Order Completed " + orderHeader.Id.ToString(), "order has been completed");
            return RedirectToAction("OrderPickup", "Order");
        }
    }
}