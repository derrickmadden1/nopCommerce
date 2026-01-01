using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Plugin.Widgets.Swiper.Domain;
using Nop.Plugin.Widgets.Swiper.Infrastructure.Cache;
using Nop.Plugin.Widgets.Swiper.Models;
using Nop.Services.Configuration;
using Nop.Services.Media;
using System.IO;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Widgets.Swiper.Components;

public class WidgetSwiperViewComponent : NopViewComponent
{
    #region Fields

    protected readonly IPictureService _pictureService;
    protected readonly IStaticCacheManager _staticCacheManager;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public WidgetSwiperViewComponent(IPictureService pictureService,
    IStaticCacheManager staticCacheManager,
    ISettingService settingService,
    IStoreContext storeContext,
    IWebHelper webHelper)
    {
        _pictureService = pictureService;
        _staticCacheManager = staticCacheManager;
        _settingService = settingService;
        _storeContext = storeContext;
        _webHelper = webHelper;
    }

    #endregion

    #region Utilities

    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task<string> GetPictureUrlAsync(int pictureId)
    {
        if (pictureId == 0)
            return string.Empty;

        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(ModelCacheEventConsumer.PictureUrlModelKey,
            pictureId, _webHelper.IsCurrentConnectionSecured());

        return await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            //little hack here. nulls aren't cacheable so set it to ""
            var url = await _pictureService.GetPictureUrlAsync(pictureId, showDefaultPicture: false) ?? "";
            return url;
        });
    }

    // best-effort: parse common image headers to get intrinsic dimensions without adding new package refs
    private static bool TryGetImageSize(byte[] data, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (data == null || data.Length < 10)
            return false;

        // PNG: 8-byte signature, IHDR chunk follows; width/height are 4-byte big-endian at offset 16
        if (data.Length >= 24 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
        {
            try
            {
                width = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
                height = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
                if (width > 0 && height > 0) return true;
            }
            catch { }
        }

        // GIF: header 'GIF87a' or 'GIF89a', width/height are little-endian uint16 at offset 6
        if (data.Length >= 10 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F')
        {
            try
            {
                width = data[6] | (data[7] << 8);
                height = data[8] | (data[9] << 8);
                if (width > 0 && height > 0) return true;
            }
            catch { }
        }

        // BMP: 'BM' and little-endian width/height at offset 18 (width) and 22 (height) for BITMAPINFOHEADER
        if (data.Length >= 26 && data[0] == 'B' && data[1] == 'M')
        {
            try
            {
                width = BitConverter.ToInt32(data, 18);
                height = Math.Abs(BitConverter.ToInt32(data, 22));
                if (width > 0 && height > 0) return true;
            }
            catch { }
        }

        // JPEG: need to scan for SOFn markers
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xD8)
        {
            try
            {
                int index = 2;
                while (index + 1 < data.Length)
                {
                    if (data[index] != 0xFF)
                    {
                        index++;
                        continue;
                    }

                    byte marker = data[index + 1];
                    // SOF0(0xC0), SOF1(0xC1), SOF2(0xC2), SOF3(0xC3), SOF5,6,7,9,10,11,13,14,15 are all possible
                    if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                    {
                        if (index + 5 >= data.Length) break;
                        int blockLength = (data[index + 2] << 8) | data[index + 3];
                        if (index + 5 + 4 > data.Length) break;
                        // sample precision = data[index+4]; next two bytes = height, then width
                        height = (data[index + 5] << 8) | data[index + 6];
                        width = (data[index + 7] << 8) | data[index + 8];
                        if (width > 0 && height > 0) return true;
                        break;
                    }
                    else
                    {
                        // skip this block
                        if (index + 3 >= data.Length) break;
                        int blockLength = (data[index + 2] << 8) | data[index + 3];
                        if (blockLength < 2) break;
                        index += 2 + blockLength;
                    }
                }
            }
            catch { }
        }

        return false;
    }

    #endregion

    #region Methods

    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var sliderSettings = await _settingService.LoadSettingAsync<SwiperSettings>(store.Id);

        if (string.IsNullOrEmpty(sliderSettings.Slides))
            return Content("");

        var model = new PublicInfoModel
        {
            ShowNavigation = sliderSettings.ShowNavigation,
            ShowPagination = sliderSettings.ShowPagination,
            Autoplay = sliderSettings.Autoplay,
            AutoplayDelay = sliderSettings.AutoplayDelay,
        };

        var slides = JsonConvert.DeserializeObject<List<Slide>>(sliderSettings.Slides);
        foreach (var slide in slides)
        {
            var picUrl = await GetPictureUrlAsync(slide.PictureId);
            if (string.IsNullOrEmpty(picUrl))
                continue;

            // attempt to get picture metadata (width/height) and build a responsive srcset
            int? width = null;
            int? height = null;
            string srcSet = null;

            try
            {
                var picture = await _pictureService.GetPictureByIdAsync(slide.PictureId);
                if (picture != null)
                {
                    try
                    {
                        // try to load picture bytes (from DB or file system depending on settings)
                        var bytes = await _pictureService.LoadPictureBinaryAsync(picture);
                        if (bytes != null && bytes.Length > 0)
                        {
                            if (TryGetImageSize(bytes, out var w, out var h))
                            {
                                width = w;
                                height = h;
                            }
                        }
                    }
                    catch
                    {
                        // ignore binary loading errors
                    }
                }

                // Build srcset by requesting common responsive sizes
                var sizes = new[] { 400, 800, 1200 };
                var parts = new List<string>();
                foreach (var s in sizes)
                {
                    var sizedUrl = await _pictureService.GetPictureUrlAsync(slide.PictureId, s, showDefaultPicture: false);
                    if (!string.IsNullOrEmpty(sizedUrl))
                        parts.Add($"{sizedUrl} {s}w");
                }
                if (parts.Any())
                    srcSet = string.Join(", ", parts);
            }
            catch
            {
                // ignore errors building srcset or retrieving picture; we still render the base PictureUrl
            }

            model.Slides.Add(new()
            {
                PictureUrl = picUrl,
                TitleText = slide.TitleText,
                LinkUrl = slide.LinkUrl,
                AltText = slide.AltText,
                LazyLoading = sliderSettings.LazyLoading,
                Width = width,
                Height = height,
                SrcSet = srcSet
            });
        }

        if (!model.Slides.Any())
            return Content("");

        return View("~/Plugins/Widgets.Swiper/Views/PublicInfo.cshtml", model);
    }

    #endregion
}