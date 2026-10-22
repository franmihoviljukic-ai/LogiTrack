# LogiTrack authentication

Run in Development using the existing launch profile (`dotnet run`). The JWT signing key is stored in appsettings.Development.json, which is excluded by .gitignore. Keep this local file private and do not commit it. If already tracked by Git, remove it from tracking before committing. JWT issuer and audience are in appsettings.json. Other environments must provide Jwt__Key (at least 32 bytes) through secure configuration.

1. POST /api/auth/register with email and password. Identity applies its password rules. Registration creates a regular user and returns 201.
2. POST /api/auth/login with the same credentials. The response contains accessToken, tokenType and expiresAtUtc. Tokens expire after 60 minutes.
3. In Postman, choose Authorization > Bearer Token and paste accessToken. The inventory and order controllers require a valid token. LogiTrack.http contains example requests.
4. DELETE operations on both controllers require the Manager role. Ordinary users receive 403; missing/invalid tokens receive 401.

To grant Manager to a registered account, stop the application and run locally from the project folder:

```powershell
dotnet run -- --assign-manager user@example.com
```

The command assigns the role and exits. Start the application again and log in again to obtain a token containing the Manager role. Public registration cannot assign roles. No default administrator or password is created.

These authorization rules grant any signed-in user GET/POST access across inventory and orders; they do not implement per-user order ownership.