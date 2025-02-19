using System.Text.Json;
using System.Text.Json.Serialization;

// ReSharper disable InconsistentNaming

namespace Script.LightweightDriver.Events
{
    internal static class PageEvents
    {

        internal delegate void FrameNavigated(string? url, string frameId);
        internal static event FrameNavigated? OnFrameNavigation;
        internal static event FrameNavigated? OnDocumentNavigate;

        internal delegate void FrameLoaded(string frameId);
        internal static event FrameLoaded? OnFrameLoaded;

        internal static Task HandlePageEvent(string message)
        {
            if (message.Contains("Page.frameNavigated"))
            {
                FrameNavigated_Root? frameNavigatedRoot = JsonSerializer.Deserialize<FrameNavigated_Root>(message);
                if (frameNavigatedRoot is not null) 
                    OnFrameNavigation?.Invoke(frameNavigatedRoot.FrameNavigatedParams.Frame.Url, frameNavigatedRoot.FrameNavigatedParams.Frame.Id);
            }
            else if (message.Contains("Page.frameStoppedLoading"))
            {
                FrameStoppedLoading_Root? frameStoppedLoadingRoot = JsonSerializer.Deserialize<FrameStoppedLoading_Root>(message);
                if (frameStoppedLoadingRoot is not null)
                {
                    OnFrameLoaded?.Invoke(frameStoppedLoadingRoot.Params.FrameId);
                }
            }
            else if (message.Contains("Page.navigatedWithinDocument"))
            {
                NavigatedWithinDocument_Root? navigatedWithinDocumentRoot = JsonSerializer.Deserialize<NavigatedWithinDocument_Root>(message);
                if (navigatedWithinDocumentRoot is not null)
                {
                    OnDocumentNavigate?.Invoke(navigatedWithinDocumentRoot.Params.Url, navigatedWithinDocumentRoot.Params.FrameId);
                }
            }

            return Task.CompletedTask;
        }
    }

    // Page.frameNavigated
    public record FrameNavigated_AdFrameStatus(
        [property: JsonPropertyName("adFrameType")] string AdFrameType
    );

    public record FrameNavigated_Frame(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("loaderId")] string LoaderId,
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("domainAndRegistry")] string DomainAndRegistry,
        [property: JsonPropertyName("securityOrigin")] string SecurityOrigin,
        [property: JsonPropertyName("mimeType")] string MimeType,
        [property: JsonPropertyName("adFrameStatus")] FrameNavigated_AdFrameStatus AdFrameStatus,
        [property: JsonPropertyName("secureContextType")] string SecureContextType,
        [property: JsonPropertyName("crossOriginIsolatedContextType")] string CrossOriginIsolatedContextType,
        [property: JsonPropertyName("gatedAPIFeatures")] IReadOnlyList<object> GatedAPIFeatures
    );

    public record FrameNavigated_Params(
        [property: JsonPropertyName("frame")] FrameNavigated_Frame Frame,
        [property: JsonPropertyName("type")] string Type
    );

    public record FrameNavigated_Root(
        [property: JsonPropertyName("method")] string Method,
        [property: JsonPropertyName("params")] FrameNavigated_Params FrameNavigatedParams
    );

    // Page.frameStoppedLoading
    public record FrameStoppedLoading_Params(
        [property: JsonPropertyName("frameId")] string FrameId
    );

    public record FrameStoppedLoading_Root(
        [property: JsonPropertyName("method")] string Method,
        [property: JsonPropertyName("params")] FrameStoppedLoading_Params Params
    );

    // Page.navigatedWithinDocument 
    public record NavigatedWithinDocument_Params(
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("navigationType")] string NavigationType,
        [property: JsonPropertyName("frameId")] string FrameId
    );

    public record NavigatedWithinDocument_Root(
        [property: JsonPropertyName("method")] string Method,
        [property: JsonPropertyName("params")] NavigatedWithinDocument_Params Params
    );
}
