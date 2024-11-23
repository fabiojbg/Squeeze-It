using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqueezeIt.Extensions
{
    public static class ImageExtensions
    {
        /// <summary>
        /// System.Drawing.Image informs wrong dimensions if the image is rotated
        /// This extension returns the corrected dimensions
        /// </summary>
        /// <param name="img"></param>
        /// <returns></returns>
        public static (int width, int height, bool rotated) GetCorrectedDimensions(this System.Drawing.Image img)
        {
            int width = img.Width;
            int height = img.Height;
            bool rotated = false;
            var rotateInfo = img.PropertyItems.FirstOrDefault(p => p.Id == 274)?.Value[0];
            if (rotateInfo == 6 || rotateInfo == 8)// image is rotated by 90 or 270 degrees? => invert dimensions
            {
                width = img.Height;
                height = img.Width;
                rotated = true;
            }
            return (width, height, rotated);
        }

    }
}
