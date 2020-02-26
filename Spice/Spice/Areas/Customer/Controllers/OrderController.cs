using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
        public OrderController(ApplicationDbContext db)
        {
            _db = db;
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

            return View(orderDetailsVM.OrderBy(o => o.OrderHeader.PickupTime));
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
    }
}