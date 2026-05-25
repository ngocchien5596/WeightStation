using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StationApp.Application.Interfaces;
using StationApp.Application.Printing;
using StationApp.Domain.Entities;
using StationApp.Infrastructure.Persistence;

namespace StationApp.UI.Printing;

public sealed class PrintTemplateProvider : IPrintTemplateProvider
{
    private const string ProfilesFileName = "print-template-profiles.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly StationDbContext _dbContext;
    private readonly IAppConfigRepository _appConfigRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IClock _clock;

    public PrintTemplateProvider(
        StationDbContext dbContext,
        IAppConfigRepository appConfigRepository,
        ICurrentUserContext currentUserContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _appConfigRepository = appConfigRepository;
        _currentUserContext = currentUserContext;
        _clock = clock;
    }

    public Task<PrintTemplateDefinition> GetTemplateAsync(PrintDocumentKind kind, CancellationToken ct)
        => GetTemplateAsync(kind, null, ct);

    public async Task<PrintTemplateDefinition> GetTemplateAsync(PrintDocumentKind kind, string? profileKey, CancellationToken ct)
    {
        var store = await LoadStoreAsync(ct);
        var profile = ResolveProfile(store, kind, profileKey);
        return kind switch
        {
            PrintDocumentKind.WeighTicket => new PrintTemplateDefinition
            {
                Kind = kind,
                TemplateName = "WeighTicketPrintTemplate",
                PageWidthMm = 210d,
                PageHeightMm = 148.5d,
                DefaultOffsetXmm = profile.OffsetXmm,
                DefaultOffsetYmm = profile.OffsetYmm,
                ActiveProfileKey = profile.ProfileKey,
                ActiveProfileName = profile.DisplayName,
                Fields = ApplyProfileLayout(WeighTicketFields, profile)
            },
            PrintDocumentKind.DeliveryTicket => new PrintTemplateDefinition
            {
                Kind = kind,
                TemplateName = "DeliveryTicketPrintTemplate",
                PageWidthMm = 210d,
                PageHeightMm = 297d,
                DefaultOffsetXmm = profile.OffsetXmm,
                DefaultOffsetYmm = profile.OffsetYmm,
                ActiveProfileKey = profile.ProfileKey,
                ActiveProfileName = profile.DisplayName,
                Fields = ApplyProfileLayout(DeliveryTicketFields, profile)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public async Task<IReadOnlyList<PrintTemplateProfileDescriptor>> GetProfilesAsync(PrintDocumentKind kind, CancellationToken ct)
    {
        var store = await LoadStoreAsync(ct);
        var defaultKey = GetDefaultProfileKey(store, kind);
        return GetProfiles(store, kind)
            .Select(x => new PrintTemplateProfileDescriptor(x.ProfileKey, x.DisplayName, string.Equals(x.ProfileKey, defaultKey, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public async Task SaveLayoutAsync(
        PrintDocumentKind kind,
        string? profileKey,
        double offsetXmm,
        double offsetYmm,
        IReadOnlyList<PrintFieldPosition> fieldPositions,
        CancellationToken ct)
    {
        var store = await LoadStoreAsync(ct);
        var profile = ResolveProfile(store, kind, profileKey);
        profile.OffsetXmm = offsetXmm;
        profile.OffsetYmm = offsetYmm;
        profile.TemplateVersion = GetCurrentTemplateVersion(kind);
        profile.Fields = fieldPositions
            .Select(x => new PersistedPrintFieldPosition
            {
                FieldKey = x.FieldKey,
                X = x.X,
                Y = x.Y,
                Width = x.Width,
                IsEnabled = x.IsEnabled
            })
            .ToList();

        await SaveStoreAsync(store, ct);
    }

    public async Task<PrintTemplateProfileDescriptor> CreateProfileAsync(
        PrintDocumentKind kind,
        string displayName,
        double offsetXmm,
        double offsetYmm,
        IReadOnlyList<PrintFieldPosition> fieldPositions,
        CancellationToken ct)
    {
        var store = await LoadStoreAsync(ct);
        var profiles = GetProfiles(store, kind);
        var profile = new PersistedPrintTemplateProfile
        {
            ProfileKey = BuildProfileKey(kind, displayName, profiles.Select(x => x.ProfileKey)),
            DisplayName = displayName.Trim(),
            OffsetXmm = offsetXmm,
            OffsetYmm = offsetYmm,
            TemplateVersion = GetCurrentTemplateVersion(kind),
            Fields = fieldPositions.Select(x => new PersistedPrintFieldPosition
            {
                FieldKey = x.FieldKey,
                X = x.X,
                Y = x.Y,
                Width = x.Width,
                IsEnabled = x.IsEnabled
            }).ToList()
        };

        profiles.Add(profile);
        await SaveStoreAsync(store, ct);
        return new PrintTemplateProfileDescriptor(profile.ProfileKey, profile.DisplayName, false);
    }

    public async Task SetDefaultProfileAsync(PrintDocumentKind kind, string profileKey, CancellationToken ct)
    {
        var store = await LoadStoreAsync(ct);
        _ = ResolveProfile(store, kind, profileKey);
        store.DefaultProfileKeys[GetTemplatePrefix(kind)] = profileKey;
        await SaveStoreAsync(store, ct);
    }

    public async Task<string> ExportBackupAsync(CancellationToken ct)
    {
        var store = await LoadStoreAsync(ct);
        var path = GetBackupFilePath();
        var json = JsonSerializer.Serialize(store, JsonOptions);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(path, json, ct);
        return path;
    }

    private static string GetGlobalOffsetKey(PrintDocumentKind kind, bool isX)
        => kind switch
        {
            PrintDocumentKind.WeighTicket => isX ? "print_weigh_offset_x_mm" : "print_weigh_offset_y_mm",
            PrintDocumentKind.DeliveryTicket => isX ? "print_delivery_offset_x_mm" : "print_delivery_offset_y_mm",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static string GetFieldOffsetKey(PrintDocumentKind kind, string fieldKey, bool isX)
        => $"print_{GetTemplatePrefix(kind)}_field_{fieldKey}_{(isX ? "x" : "y")}_mm";

    private static string GetFieldWidthKey(PrintDocumentKind kind, string fieldKey)
        => $"print_{GetTemplatePrefix(kind)}_field_{fieldKey}_width_mm";

    private static string GetTemplatePrefix(PrintDocumentKind kind)
        => kind switch
        {
            PrintDocumentKind.WeighTicket => "weigh",
            PrintDocumentKind.DeliveryTicket => "delivery",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static string GetTemplateVersionKey(PrintDocumentKind kind)
        => $"print_{GetTemplatePrefix(kind)}_layout_version";

    private static int GetCurrentTemplateVersion(PrintDocumentKind kind)
        => kind switch
        {
            PrintDocumentKind.WeighTicket => 2,
            PrintDocumentKind.DeliveryTicket => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static string GetDefaultProfileStorageKey(PrintDocumentKind kind)
        => GetTemplatePrefix(kind);

    private async Task<PersistedPrintTemplateStore> LoadStoreAsync(CancellationToken ct)
    {
        var store = await LoadStoreFromDatabaseAsync(ct);
        var changed = false;

        if (IsStoreEmpty(store))
        {
            var importedStore = await TryLoadLegacyFileStoreAsync(ct);
            if (importedStore != null)
            {
                store = importedStore;
                changed = true;
            }
        }

        changed |= await EnsureSeedProfileAsync(store, PrintDocumentKind.WeighTicket, "PC ver 1", WeighTicketFields, ct);
        changed |= await EnsureSeedProfileAsync(store, PrintDocumentKind.DeliveryTicket, "PGN ver 1", DeliveryTicketFields, ct);

        if (changed)
        {
            await SaveStoreAsync(store, ct);
        }

        return store;
    }

    private async Task<PersistedPrintTemplateStore> LoadStoreFromDatabaseAsync(CancellationToken ct)
    {
        var store = new PersistedPrintTemplateStore();
        var rows = await _dbContext.PrintTemplateProfiles
            .AsNoTracking()
            .OrderBy(x => x.TemplateKind)
            .ThenByDescending(x => x.IsDefault)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            if (!store.ProfilesByKind.TryGetValue(row.TemplateKind, out var profiles))
            {
                profiles = [];
                store.ProfilesByKind[row.TemplateKind] = profiles;
            }

            profiles.Add(new PersistedPrintTemplateProfile
            {
                ProfileKey = row.ProfileKey,
                DisplayName = row.DisplayName,
                OffsetXmm = row.OffsetXmm,
                OffsetYmm = row.OffsetYmm,
                TemplateVersion = row.TemplateVersion,
                Fields = DeserializeFieldPositions(row.LayoutJson)
            });

            if (row.IsDefault)
            {
                store.DefaultProfileKeys[row.TemplateKind] = row.ProfileKey;
            }
        }

        return store;
    }

    private async Task<PersistedPrintTemplateStore?> TryLoadLegacyFileStoreAsync(CancellationToken ct)
    {
        var storePath = GetProfilesFilePath();
        if (!File.Exists(storePath))
        {
            return null;
        }

        var raw = await File.ReadAllTextAsync(storePath, ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return JsonSerializer.Deserialize<PersistedPrintTemplateStore>(raw, JsonOptions);
    }

    private async Task<bool> EnsureSeedProfileAsync(
        PersistedPrintTemplateStore store,
        PrintDocumentKind kind,
        string displayName,
        IReadOnlyList<PrintFieldDefinition> defaults,
        CancellationToken ct)
    {
        var profiles = GetProfiles(store, kind);
        if (profiles.Count > 0)
        {
            if (!store.DefaultProfileKeys.ContainsKey(GetDefaultProfileStorageKey(kind)))
            {
                store.DefaultProfileKeys[GetDefaultProfileStorageKey(kind)] = profiles[0].ProfileKey;
                return true;
            }

            return false;
        }

        var migrated = await MigrateLegacyProfileAsync(kind, displayName, defaults, ct);
        profiles.Add(migrated);
        store.DefaultProfileKeys[GetDefaultProfileStorageKey(kind)] = migrated.ProfileKey;
        return true;
    }

    private async Task<PersistedPrintTemplateProfile> MigrateLegacyProfileAsync(
        PrintDocumentKind kind,
        string displayName,
        IReadOnlyList<PrintFieldDefinition> defaults,
        CancellationToken ct)
    {
        var usePersistedLayout = await ShouldUsePersistedLayoutAsync(kind, ct);
        var positions = new List<PersistedPrintFieldPosition>(defaults.Count);

        foreach (var field in defaults)
        {
            var x = field.X;
            var y = field.Y;
            var width = field.Width;

            if (usePersistedLayout)
            {
                var xRaw = await _appConfigRepository.GetValueAsync(GetFieldOffsetKey(kind, field.FieldKey, isX: true), ct);
                var yRaw = await _appConfigRepository.GetValueAsync(GetFieldOffsetKey(kind, field.FieldKey, isX: false), ct);
                var widthRaw = await _appConfigRepository.GetValueAsync(GetFieldWidthKey(kind, field.FieldKey), ct);
                x = double.TryParse(xRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedX) ? parsedX : x;
                y = double.TryParse(yRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedY) ? parsedY : y;
                width = double.TryParse(widthRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedWidth) ? parsedWidth : width;
            }

            positions.Add(new PersistedPrintFieldPosition
            {
                FieldKey = field.FieldKey,
                X = x,
                Y = y,
                Width = width,
                IsEnabled = field.IsEnabled
            });
        }

        return new PersistedPrintTemplateProfile
        {
            ProfileKey = BuildProfileKey(kind, displayName, Array.Empty<string>()),
            DisplayName = displayName,
            OffsetXmm = usePersistedLayout ? await GetLegacyOffsetAsync(kind, true, ct) : 0d,
            OffsetYmm = usePersistedLayout ? await GetLegacyOffsetAsync(kind, false, ct) : 0d,
            TemplateVersion = GetCurrentTemplateVersion(kind),
            Fields = positions
        };
    }

    private async Task<double> GetLegacyOffsetAsync(PrintDocumentKind kind, bool isX, CancellationToken ct)
    {
        var raw = await _appConfigRepository.GetValueAsync(GetGlobalOffsetKey(kind, isX), ct);
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0d;
    }

    private async Task<bool> ShouldUsePersistedLayoutAsync(PrintDocumentKind kind, CancellationToken ct)
    {
        var raw = await _appConfigRepository.GetValueAsync(GetTemplateVersionKey(kind), ct);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var savedVersion)
               && savedVersion >= GetCurrentTemplateVersion(kind);
    }

    private static IReadOnlyList<PrintFieldDefinition> ApplyProfileLayout(
        IReadOnlyList<PrintFieldDefinition> defaults,
        PersistedPrintTemplateProfile profile)
    {
        var positions = profile.Fields.ToDictionary(x => x.FieldKey, StringComparer.OrdinalIgnoreCase);
        return defaults
            .Select(field => positions.TryGetValue(field.FieldKey, out var pos)
                ? field with
                {
                    X = pos.X,
                    Y = pos.Y,
                    Width = pos.Width ?? field.Width,
                    IsEnabled = pos.IsEnabled
                }
                : field)
            .ToList();
    }

    private static PersistedPrintTemplateProfile ResolveProfile(PersistedPrintTemplateStore store, PrintDocumentKind kind, string? profileKey)
    {
        var profiles = GetProfiles(store, kind);
        var effectiveKey = string.IsNullOrWhiteSpace(profileKey) ? GetDefaultProfileKey(store, kind) : profileKey;
        return profiles.FirstOrDefault(x => string.Equals(x.ProfileKey, effectiveKey, StringComparison.OrdinalIgnoreCase))
            ?? profiles.First();
    }

    private static List<PersistedPrintTemplateProfile> GetProfiles(PersistedPrintTemplateStore store, PrintDocumentKind kind)
    {
        var storageKey = GetDefaultProfileStorageKey(kind);
        if (!store.ProfilesByKind.TryGetValue(storageKey, out var profiles))
        {
            profiles = [];
            store.ProfilesByKind[storageKey] = profiles;
        }

        return profiles;
    }

    private static string GetDefaultProfileKey(PersistedPrintTemplateStore store, PrintDocumentKind kind)
    {
        var storageKey = GetDefaultProfileStorageKey(kind);
        return store.DefaultProfileKeys.TryGetValue(storageKey, out var key) && !string.IsNullOrWhiteSpace(key)
            ? key
            : GetProfiles(store, kind).First().ProfileKey;
    }

    private async Task SaveStoreAsync(PersistedPrintTemplateStore store, CancellationToken ct)
    {
        var existing = await _dbContext.PrintTemplateProfiles.ToListAsync(ct);
        if (existing.Count > 0)
        {
            _dbContext.PrintTemplateProfiles.RemoveRange(existing);
        }

        var actor = string.IsNullOrWhiteSpace(_currentUserContext.Username) ? "SYSTEM" : _currentUserContext.Username;
        var now = _clock.NowLocal;
        var rows = new List<PrintTemplateProfile>();

        foreach (var (kindKey, profiles) in store.ProfilesByKind)
        {
            var defaultKey = store.DefaultProfileKeys.TryGetValue(kindKey, out var key) ? key : profiles.FirstOrDefault()?.ProfileKey;
            rows.AddRange(profiles.Select(profile => new PrintTemplateProfile
            {
                Id = Guid.NewGuid(),
                TemplateKind = kindKey,
                ProfileKey = profile.ProfileKey,
                DisplayName = profile.DisplayName,
                IsDefault = string.Equals(profile.ProfileKey, defaultKey, StringComparison.OrdinalIgnoreCase),
                OffsetXmm = profile.OffsetXmm,
                OffsetYmm = profile.OffsetYmm,
                TemplateVersion = profile.TemplateVersion,
                LayoutJson = JsonSerializer.Serialize(profile.Fields, JsonOptions),
                CreatedAt = now,
                CreatedBy = actor,
                UpdatedAt = now,
                UpdatedBy = actor
            }));
        }

        if (rows.Count > 0)
        {
            await _dbContext.PrintTemplateProfiles.AddRangeAsync(rows, ct);
        }

        await _dbContext.SaveChangesAsync(ct);
        await ExportBackupAsync(ct);
    }

    private static bool IsStoreEmpty(PersistedPrintTemplateStore store)
        => store.ProfilesByKind.Values.All(x => x.Count == 0);

    private static List<PersistedPrintFieldPosition> DeserializeFieldPositions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<PersistedPrintFieldPosition>>(json, JsonOptions) ?? [];
    }

    private static string BuildProfileKey(PrintDocumentKind kind, string displayName, IEnumerable<string> existingKeys)
    {
        var prefix = GetTemplatePrefix(kind);
        var baseKey = string.Concat(displayName
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'))
            .Trim('-');
        if (string.IsNullOrWhiteSpace(baseKey))
        {
            baseKey = $"{prefix}-ver";
        }

        var key = $"{prefix}-{baseKey}";
        var set = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);
        if (!set.Contains(key))
        {
            return key;
        }

        var index = 2;
        while (set.Contains($"{key}-{index}"))
        {
            index++;
        }

        return $"{key}-{index}";
    }

    private static string GetBackupFilePath()
        => Path.Combine(AppContext.BaseDirectory, "print-layout-backup.json");

    private static string GetProfilesFilePath()
        => Path.Combine(AppContext.BaseDirectory, ProfilesFileName);

    private sealed class PersistedPrintTemplateStore
    {
        public Dictionary<string, List<PersistedPrintTemplateProfile>> ProfilesByKind { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> DefaultProfileKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class PersistedPrintTemplateProfile
    {
        public string ProfileKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public double OffsetXmm { get; set; }
        public double OffsetYmm { get; set; }
        public int TemplateVersion { get; set; }
        public List<PersistedPrintFieldPosition> Fields { get; set; } = [];
    }

    private sealed class PersistedPrintFieldPosition
    {
        public string FieldKey { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double? Width { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    private static readonly IReadOnlyList<PrintFieldDefinition> WeighTicketFields =
    [
        new("StaticCompanyLogo", 32, 7.5, 12, PrintFieldAlignment.Left, 7, PrintFieldWeight.Normal, IsImage: true, ImageSourceUri: "pack://application:,,,/Assets/logo.jpg"),
        new("StaticCompanyName", 108, 8, 58, PrintFieldAlignment.Center, 8.8, PrintFieldWeight.Bold, LiteralValue: "C\u00d4NG TY C\u1ed4 PH\u1ea6N XI M\u0102NG C\u1ea8M PH\u1ea2", Underline: true),
        new("StaticCompanyAddress", 104, 16, 68, PrintFieldAlignment.Center, 7.1, PrintFieldWeight.Normal, LiteralValue: "\u0110C: Km6, Qu\u1ed1c l\u1ed9 18A, Ph\u01b0\u1eddng Quang Hanh, T\u1ec9nh Qu\u1ea3ng Ninh", Italic: true),
        new("StaticCompanyPhone", 107, 22, 62, PrintFieldAlignment.Center, 7.1, PrintFieldWeight.Normal, LiteralValue: "\u0110T: (020)33.721.995  -  (020)33.721.996", Italic: true),
        new("StaticTitle", 164, 7, 32, PrintFieldAlignment.Center, 16.5, PrintFieldWeight.Bold, LiteralValue: "PHI\u1ebeU C\u00c2N", Underline: true),
        new("StaticTicketLabel", 149, 22, 18, PrintFieldAlignment.Right, 9.2, PrintFieldWeight.Bold, LiteralValue: "S\u1ed1 phi\u1ebfu:", ShadedBackground: true),
        new("TicketNo", 168, 22, 22, PrintFieldAlignment.Left, 9.8, PrintFieldWeight.Bold, ShadedBackground: true),
        new("StaticVehicleSectionLine", 14, 28, 182, PrintFieldAlignment.Left, 8.5, PrintFieldWeight.Normal, IsLine: true),

        new("StaticVehiclePlateLabel", 32, 32, 18, PrintFieldAlignment.Left, 9.6, PrintFieldWeight.Bold, LiteralValue: "Bi\u1ec3n s\u1ed1 xe:"),
        new("VehiclePlate", 52, 32, 78, PrintFieldAlignment.Left, 11.5, PrintFieldWeight.Bold),
        new("StaticWeight1Label", 164, 32, 13, PrintFieldAlignment.Right, 9.4, PrintFieldWeight.Normal, LiteralValue: "Gi\u1edd v\u00e0o:"),
        new("Weight1DateTime", 182, 32, 29, PrintFieldAlignment.Left, 9.8, PrintFieldWeight.Normal),

        new("StaticVehicleRegistrationLabel", 32, 42, 16, PrintFieldAlignment.Left, 9.6, PrintFieldWeight.Bold, LiteralValue: "Tem xe:"),
        new("VehicleRegistrationNo", 52, 42, 60, PrintFieldAlignment.Left, 10.4, PrintFieldWeight.Bold),
        new("StaticWeight2Label", 164, 42, 11, PrintFieldAlignment.Right, 9.4, PrintFieldWeight.Normal, LiteralValue: "Gi\u1edd ra:"),
        new("Weight2DateTime", 182, 42, 29, PrintFieldAlignment.Left, 9.8, PrintFieldWeight.Normal),

        new("StaticMoocRegistrationLabel", 32, 52, 18, PrintFieldAlignment.Left, 9.6, PrintFieldWeight.Bold, LiteralValue: "Tem mo\u00f3c:"),
        new("MoocRegistrationNo", 52, 52, 60, PrintFieldAlignment.Left, 10.4, PrintFieldWeight.Bold),
        new("StaticGrossWeightLabel", 157, 52, 24, PrintFieldAlignment.Right, 9.4, PrintFieldWeight.Normal, LiteralValue: "Tr\u1ecdng l\u01b0\u1ee3ng t\u1ed5ng (t\u1ea5n):"),
        new("GrossWeight", 199, 52, 12, PrintFieldAlignment.Left, 11.8, PrintFieldWeight.Bold),

        new("StaticProductLabel", 32, 62, 16, PrintFieldAlignment.Left, 9.6, PrintFieldWeight.Bold, LiteralValue: "H\u00e0ng h\u00f3a:"),
        new("ProductName", 52, 62, 82, PrintFieldAlignment.Left, 10.6, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),
        new("StaticEmptyWeightLabel", 157, 62, 22, PrintFieldAlignment.Right, 9.4, PrintFieldWeight.Normal, LiteralValue: "Tr\u1ecdng l\u01b0\u1ee3ng xe (t\u1ea5n):"),
        new("EmptyWeight", 199, 62, 12, PrintFieldAlignment.Left, 11.8, PrintFieldWeight.Bold),

        new("StaticLotNoLabel", 32, 75, 14, PrintFieldAlignment.Left, 9.6, PrintFieldWeight.Bold, LiteralValue: "L\u00f4 h\u00e0ng:"),
        new("LotNo", 52, 75, 40, PrintFieldAlignment.Left, 10.2, PrintFieldWeight.Normal),
        new("StaticNetWeightLabel", 151, 75, 28, PrintFieldAlignment.Right, 9.4, PrintFieldWeight.Normal, LiteralValue: "Tr\u1ecdng l\u01b0\u1ee3ng h\u00e0ng (t\u1ea5n):"),
        new("NetWeight", 199, 75, 12, PrintFieldAlignment.Left, 11.8, PrintFieldWeight.Bold),

        new("StaticCustomerLabel", 32, 85, 18, PrintFieldAlignment.Left, 9.6, PrintFieldWeight.Bold, LiteralValue: "Kh\u00e1ch h\u00e0ng:"),
        new("CustomerName", 52, 85, 82, PrintFieldAlignment.Left, 10.8, PrintFieldWeight.Bold, 2, PrintWrapMode.Wrap),
        new("StaticNotesLabel", 170, 85, 10, PrintFieldAlignment.Right, 9.4, PrintFieldWeight.Normal, LiteralValue: "Ghi ch\u00fa:"),
        new("Notes", 182, 85, 30, PrintFieldAlignment.Left, 9.6, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),

        new("StaticRepresentativeLabel", 32, 101, 16, PrintFieldAlignment.Left, 9.6, PrintFieldWeight.Bold, LiteralValue: "\u0110\u1ea1i di\u1ec7n:"),
        new("RepresentativeName", 52, 101, 68, PrintFieldAlignment.Left, 10.2, PrintFieldWeight.Normal),
        new("StaticPrintedAtLabel", 161, 101, 18, PrintFieldAlignment.Right, 9.4, PrintFieldWeight.Normal, LiteralValue: "Ng\u00e0y in phi\u1ebfu:", Italic: true),
        new("PrintedAt", 182, 101, 29, PrintFieldAlignment.Left, 9.8, PrintFieldWeight.Normal, Italic: true),

        new("StaticSignerLine", 30, 113, 182, PrintFieldAlignment.Left, 8.5, PrintFieldWeight.Normal, IsLine: true),
        new("StaticSigner1", 39, 119, 34, PrintFieldAlignment.Center, 9.8, PrintFieldWeight.Bold, LiteralValue: "\u0110\u1ea1i di\u1ec7n giao nh\u1eadn"),
        new("StaticSigner2", 93, 119, 18, PrintFieldAlignment.Center, 9.8, PrintFieldWeight.Bold, LiteralValue: "L\u00e1i xe"),
        new("StaticSigner3", 135, 119, 20, PrintFieldAlignment.Center, 9.8, PrintFieldWeight.Bold, LiteralValue: "Ki\u1ec3m so\u00e1t"),
        new("StaticSigner4", 175, 119, 24, PrintFieldAlignment.Center, 9.8, PrintFieldWeight.Bold, LiteralValue: "Nh\u00e2n vi\u00ean c\u00e2n"),
        new("PrintedBy", 172, 138, 30, PrintFieldAlignment.Center, 9.8, PrintFieldWeight.Bold, Italic: true),
        new("StaticPrintedByUnderline", 172, 144, 30, PrintFieldAlignment.Left, 8.5, PrintFieldWeight.Normal, IsLine: true),

        new("StaticFooterLeft", 20, 132, 32, PrintFieldAlignment.Left, 7.2, PrintFieldWeight.Bold, LiteralValue: "XMCP c\u00e2n 120 t\u1ea5n - C2"),
        new("StaticFooterRight", 70, 141, 100, PrintFieldAlignment.Center, 7.2, PrintFieldWeight.Normal, LiteralValue: "Copyright (2026) by CAMPHACEMENT - www.camphacement.vn")
    ];

    private static readonly IReadOnlyList<PrintFieldDefinition> DeliveryTicketFields =
    [
        new("DeliveryNo", 150, 39, 34, PrintFieldAlignment.Left, 11.5, PrintFieldWeight.Normal),
        new("ReferenceCode", 150, 54, 34, PrintFieldAlignment.Left, 11.5, PrintFieldWeight.Normal),
        new("CustomerName", 27, 86, 156, PrintFieldAlignment.Left, 11.5, PrintFieldWeight.Normal),
        new("ConsumptionPlace", 27, 104, 103, PrintFieldAlignment.Left, 11.5, PrintFieldWeight.Normal),
        new("LoadingPlace", 27, 122, 103, PrintFieldAlignment.Left, 11.5, PrintFieldWeight.Normal),
        new("CustomerCode", 147, 122, 34, PrintFieldAlignment.Left, 11.5, PrintFieldWeight.Normal),
        new("ProductName", 25, 149, 42, PrintFieldAlignment.Left, 11.5, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),
        new("PlannedWeight", 79, 149, 14, PrintFieldAlignment.Center, 11.5, PrintFieldWeight.Normal),
        new("BagCount", 95, 149, 14, PrintFieldAlignment.Center, 11.5, PrintFieldWeight.Normal),
        new("ActualWeight", 118, 149, 14, PrintFieldAlignment.Center, 11.5, PrintFieldWeight.Normal),
        new("ActualBagCount", 134, 149, 14, PrintFieldAlignment.Center, 11.5, PrintFieldWeight.Normal),
        new("LotNo", 155, 149, 17, PrintFieldAlignment.Center, 11.5, PrintFieldWeight.Normal),
        new("VehicleLine", 176, 146, 18, PrintFieldAlignment.Left, 11.5, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),
        new("SealNo", 31, 214, 56, PrintFieldAlignment.Left, 11.5, PrintFieldWeight.Normal),
        new("Weight1Hour", 121, 233, 8, PrintFieldAlignment.Center, 11.5, PrintFieldWeight.Normal),
        new("Weight1Minute", 142, 233, 8, PrintFieldAlignment.Center, 11.5, PrintFieldWeight.Normal),
        new("Weight1Date", 161, 233, 24, PrintFieldAlignment.Left, 11.5, PrintFieldWeight.Normal),
        new("Weight2Hour", 121, 249, 8, PrintFieldAlignment.Center, 11.5, PrintFieldWeight.Normal),
        new("Weight2Minute", 142, 249, 8, PrintFieldAlignment.Center, 11.5, PrintFieldWeight.Normal),
        new("Weight2Date", 161, 249, 24, PrintFieldAlignment.Left, 11.5, PrintFieldWeight.Normal),
        new("Notes", 18, 267, 166, PrintFieldAlignment.Left, 11.5, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),
        new("PrintedBy", 24, 287, 46, PrintFieldAlignment.Center, 11.5, PrintFieldWeight.Normal)
    ];
}





