using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Spice.Data;
using Spice.Models;

namespace Spice.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CouponController : Controller
    {
        private readonly ApplicationDbContext _db;
        public CouponController(ApplicationDbContext db)
        {
            _db = db;
        }
        // Get index of all coupons
        public async Task<IActionResult> Index()
        {
            return View(await _db.Coupon.ToListAsync());
        }
        // Get create
        public IActionResult Create()
        {
            return View();
        }
        // Post create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Coupon coupons)
        {
            if (ModelState.IsValid)
            {
                var files = HttpContext.Request.Form.Files;
                if (files.Count > 0)
                {
                    byte[] p1 = null;
                    using(var fs1 = files[0].OpenReadStream())
                    {
                        using(var ms1 = new MemoryStream())
                        {
                            fs1.CopyTo(ms1);
                            p1 = ms1.ToArray();
                        }
                    }
                    coupons.Picture = p1;
                }
                _db.Coupon.Add(coupons);
                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(coupons);
        }

        // Get Edit
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            Coupon coupon = await _db.Coupon.FirstOrDefaultAsync(c => c.Id == id);
            if (coupon == null)
            {
                return NotFound();
            }

            return View(coupon);
        }
        // Post Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int? id, Coupon coupon)
        {
            if (id == null)
            {
                return NotFound();
            }
            // we need AsNoTracking here to prevent tracking errors
            var couponFromDb = await _db.Coupon.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (ModelState.IsValid)
            {
                var files = HttpContext.Request.Form.Files;
                if (files.Count > 0)
                {
                    byte[] p1 = null;
                    using(var fs1 = files[0].OpenReadStream())
                    {
                        using(var ms1 = new MemoryStream())
                        {
                            fs1.CopyTo(ms1);
                            p1 = ms1.ToArray();
                        }
                    }
                    coupon.Picture = p1;
                }
                else
                {
                    coupon.Picture = couponFromDb.Picture;
                }
                _db.Coupon.Update(coupon);
                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(coupon);
        }
        // Get Details
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            Coupon coupon = await _db.Coupon.FirstOrDefaultAsync(c => c.Id == id);
            if (coupon == null)
            {
                return NotFound();
            }

            return View(coupon);
        }
    }
}