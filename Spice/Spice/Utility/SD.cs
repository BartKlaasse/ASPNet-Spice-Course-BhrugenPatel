using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Spice.Models;

namespace Spice.Utility
{
    public static class SD
    {
        public const string DefaultFoodImage = "default_food.png";
        public const string ManagerUser = "Manager";
        public const string KitchenUser = "Kitchen";
        public const string FrontDeskUser = "FrontDesk";
        public const string CustomerEndUser = "Customer";

        public const string sessionShoppingCartCount = "sessionCartCount";
        public const string sessionCouponCode = "sessionCouponCode";
        public const string StatusSubmitted = "Submitted";
        public const string StatusInProcess = "Being Prepared.";
        public const string StatusReady = "Ready for pickup";
        public const string StatusCompleted = "Completed";
        public const string StatusCancelled = "Cancelled";
        public const string PaymentStatusPending = "Pending";
        public const string PaymentStatusApproved = "Approved";
        public const string PaymentStatusRejected = "Rejected";

        public static string ConvertToRawHtml(string source)
        {
            char[] array = new char[source.Length];
            int arrayIndex = 0;
            bool inside = false;

            for (int i = 0; i < source.Length; i++)
            {
                char
                let = source[i];
                if (let == '<')
                {
                    inside = true;
                    continue;
                }
                if (let == '>')
                {
                    inside = false;
                    continue;
                }
                if (!inside)
                {
                    array[arrayIndex] =
                        let;
                    arrayIndex++;
                }
            }
            return new string(array, 0, arrayIndex);
        }

        public static double DiscountedPrice(Coupon couponFromDB, double OriginalOrderTotal)
        {
            if (couponFromDB == null)
            {
                // coupon does not exist
                return OriginalOrderTotal;
            }

            if (couponFromDB.MinimumAmount > OriginalOrderTotal)
            {
                // coupon voldoet niet aan minumum amount
                return OriginalOrderTotal;
            }

            // coupon exists and order total is higher then minimum amount
            if (Convert.ToInt32(couponFromDB.CouponType) == (int) Coupon.ECouponType.Dollar)
            {
                // 10$ discount on order amount of 100$
                return Math.Round(OriginalOrderTotal - couponFromDB.Discount, 2);
            }

            if (Convert.ToInt32(couponFromDB.CouponType) == (int) Coupon.ECouponType.Percent)
            {
                // 10% discount on order amount of 100$
                return Math.Round(OriginalOrderTotal - (OriginalOrderTotal * couponFromDB.Discount / 100), 2);
            }
            return OriginalOrderTotal;
        }
    }
}