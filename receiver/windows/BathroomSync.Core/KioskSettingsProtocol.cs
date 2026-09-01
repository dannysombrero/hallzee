namespace BathroomSync.Core;

public static class KioskSettingsProtocol {
  public const int MinimumStudentIdLength = 4;
  public const int DefaultStudentIdLength = 10;
  public const int MaximumStudentIdLength = 16;
  public const string QueryCommand = "GET_SETTINGS";

  public static string BuildStudentIdLengthCommand(int value) {
    if (value < MinimumStudentIdLength || value > MaximumStudentIdLength)
      throw new ArgumentOutOfRangeException(nameof(value));
    return $"SET,MAX_ID_LENGTH,{value}";
  }
}
