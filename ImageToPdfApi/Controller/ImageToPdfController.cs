using Microsoft.AspNetCore.Mvc;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;

namespace ImageToPdfApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImageToPdfController : ControllerBase
    {
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
        private const int MaxFileSizeInMb = 10;
        private const int MaxFileSizeInBytes = MaxFileSizeInMb * 1024 * 1024;

        [HttpPost("convert")]
        public async Task<IActionResult> ConvertToPdf([FromForm] IFormFile image)
        {
            try
            {
                if (image == null || image.Length == 0)
                    return BadRequest("No image uploaded.");

                // Validate file size
                if (image.Length > MaxFileSizeInBytes)
                    return BadRequest($"File size exceeds maximum limit of {MaxFileSizeInMb}MB.");

                // Validate file extension
                var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
                if (!_allowedExtensions.Contains(extension))
                    return BadRequest("Invalid file type. Supported types: JPG, PNG, GIF.");

                using var memoryStream = new MemoryStream();
                await image.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // Load image using ImageSharp
                using var img = Image.Load<Rgba32>(memoryStream);

                // Convert to bitmap for PdfSharpCore
                using var ms = new MemoryStream();
                img.SaveAsPng(ms);
                ms.Position = 0;

                var document = new PdfDocument();
                var page = document.AddPage();

                using var gfx = XGraphics.FromPdfPage(page);
                using var imageSharp = XImage.FromStream(() => ms);

                // Scale to fit page
                double widthRatio = page.Width / imageSharp.PixelWidth;
                double heightRatio = page.Height / imageSharp.PixelHeight;
                double scale = Math.Min(widthRatio, heightRatio);

                double width = imageSharp.PixelWidth * scale;
                double height = imageSharp.PixelHeight * scale;

                // Calculate position to center the image
                double x = (page.Width - width) / 2;
                double y = (page.Height - height) / 2;

                gfx.DrawImage(imageSharp, x, y, width, height);

                using var output = new MemoryStream();
                document.Save(output, false);
                output.Position = 0;

                return File(output.ToArray(), "application/pdf", $"{Path.GetFileNameWithoutExtension(image.FileName)}.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}