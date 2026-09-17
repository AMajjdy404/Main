using Microsoft.AspNetCore.Http;

namespace Main.Application.Helpers
{
    public static class DocumentSettings
    {
        // Adjust per-project: this default whitelist only covers common image/document types.
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf"
        };

        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

        public static async Task<string> UploadFileAsync(IFormFile file, string folderName, string webRootPath)
        {
            if (file == null || file.Length == 0)
                throw new InvalidOperationException("الملف غير موجود أو فارغ.");

            if (file.Length > MaxFileSizeBytes)
                throw new InvalidOperationException($"حجم الملف يتجاوز الحد المسموح ({MaxFileSizeBytes / (1024 * 1024)} ميجابايت).");

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
                throw new InvalidOperationException("نوع الملف غير مسموح به.");

            var folderPath = Path.Combine(webRootPath, "Images", folderName);

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(folderPath, fileName);

            using var fileStream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(fileStream);

            return $"/Images/{folderName}/{fileName}";
        }

        public static bool DeleteFile(string fileUrl, string folderName, string webRootPath)
        {
            var folderPath = Path.Combine(webRootPath, "Images", folderName);

            var fileName = Path.GetFileName(fileUrl);
            var filePath = Path.Combine(folderPath, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }

            return false;
        }
    }
}
