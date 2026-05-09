using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string basePath = Path.GetFullPath("StorageRoot");
Directory.CreateDirectory(basePath);

string GetSafePath(string? relativePath)
{
    if (string.IsNullOrEmpty(relativePath)) return basePath;

    string fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath));

    if (!fullPath.StartsWith(basePath))
    {
        throw new BadHttpRequestException("Nedopustimiy put", 400);
    }
    return fullPath;
}

app.MapGet("/{**filepath}", (string? filepath) =>
{
    try
    {
        string fullPath = GetSafePath(filepath);

        if (Directory.Exists(fullPath))
        {
            var dirInfo = new DirectoryInfo(fullPath);
            var entries = dirInfo.GetFileSystemInfos().Select(info => new
            {
                Name = info.Name,
                IsDirectory = info.Attributes.HasFlag(FileAttributes.Directory),
                Size = info is FileInfo f ? f.Length : 0,
                LastModified = info.LastWriteTimeUtc
            });
            return Results.Ok(entries);
        }

        if (File.Exists(fullPath))
        {
            return Results.File(fullPath);
        }

        return Results.NotFound(new { Message = "Fail ili katalog ne nayden" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { ex.Message });
    }
});

app.MapPut("/{**filepath}", async (HttpRequest request, string? filepath) =>
{
    if (string.IsNullOrEmpty(filepath))
        return Results.BadRequest(new { Message = "Put k falu ne mozhet bit pustim" });

    try
    {
        string fullPath = GetSafePath(filepath);
        string? directory = Path.GetDirectoryName(fullPath);

        if (directory != null) Directory.CreateDirectory(directory);

        bool fileExisted = File.Exists(fullPath);

        using (var fileStream = new FileStream(fullPath, FileMode.Create))
        {
            await request.Body.CopyToAsync(fileStream);
        }

        return fileExisted ? Results.NoContent() : Results.Created($"/{filepath}", null);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { ex.Message });
    }
});

app.MapMethods("/{**filepath}", new[] { "HEAD" }, (HttpContext context, string? filepath) =>
{
    try
    {
        string fullPath = GetSafePath(filepath);

        if (!File.Exists(fullPath)) return Results.NotFound();

        var fileInfo = new FileInfo(fullPath);

        context.Response.Headers.ContentLength = fileInfo.Length;
        context.Response.Headers.LastModified = fileInfo.LastWriteTimeUtc.ToString("R");

        return Results.Ok();
    }
    catch
    {
        return Results.BadRequest();
    }
});

app.MapDelete("/{**filepath}", (string? filepath) =>
{
    if (string.IsNullOrEmpty(filepath))
        return Results.BadRequest(new { Message = "Nelzya udalit kornevoy katalog" });

    try
    {
        string fullPath = GetSafePath(filepath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return Results.Ok(new { Message = "Fail udalen" });
        }

        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, true);
            return Results.Ok(new { Message = "Katalog udalen" });
        }

        return Results.NotFound(new { Message = "Fail ili katalog ne nayden" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { ex.Message });
    }
});

app.Run("http://localhost:5000");