using System.Net;
using osu.Framework.Localisation;
using static osu.Game.Rulesets.Sayobot.Localisation.SayobotStrings;

namespace osu.Game.Rulesets.Sayobot.Localisation;

public static class SayobotErrors
{
    // Keep the full exception in the runtime log; show a translated explanation in the UI.
    public static LocalisableString Describe(Exception exception, bool importStarted) => importStarted
        ? Get(SayobotString.ImportError)
        : exception switch
        {
            HttpRequestException { StatusCode: HttpStatusCode.NotFound or HttpStatusCode.Gone } => Get(SayobotString.MirrorMissing),
            HttpRequestException { StatusCode: { } status } => Get(SayobotString.ServerError, (int)status),
            HttpRequestException => Get(SayobotString.NetworkError),
            TimeoutException or OperationCanceledException => Get(SayobotString.Timeout),
            InvalidDataException => Get(SayobotString.InvalidArchive),
            IOException or UnauthorizedAccessException => Get(SayobotString.FileError),
            _ => Get(SayobotString.UnknownError),
        };
}
