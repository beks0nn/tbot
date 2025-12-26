using System.Text.Json;

namespace Bot.State;

public static class ProfileStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string GetPath(string profileName)
        => Path.Combine(AppContext.BaseDirectory, "Profiles", $"{profileName}.json");

    public static ProfileSettings LoadOrCreate(string profileName)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Profiles");
        Directory.CreateDirectory(dir);

        var path = GetPath(profileName);

        if (!File.Exists(path))
        {
            var p = new ProfileSettings { ProfileName = profileName };
            Save(p);
            return p;
        }

        var json = File.ReadAllText(path);
        var profile = JsonSerializer.Deserialize<ProfileSettings>(json, JsonOpts) ?? new ProfileSettings { ProfileName = profileName };

        profile.ProfileName = string.IsNullOrWhiteSpace(profile.ProfileName) ? profileName : profile.ProfileName;
        return profile;
    }

    public static void Save(ProfileSettings profile)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Profiles");
        Directory.CreateDirectory(dir);

        var path = GetPath(profile.ProfileName);
        var json = JsonSerializer.Serialize(profile, JsonOpts);
        File.WriteAllText(path, json);
    }
}