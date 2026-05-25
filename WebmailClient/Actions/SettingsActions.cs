using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using WebmailClient.Data;
using WebmailClient.Models;
using System;

namespace WebmailClient.Actions
{
    public static class SettingsActions
    {
        public static void MapSettingsEndpoints(this IEndpointRouteBuilder routes)
        {
            var api = routes.MapGroup("/api/settings");

            api.MapPost("/", async (int accountId, UpdateSettingsRequest req, AppDbContext db) =>
            {
                var account = await db.UserAccounts.FindAsync(accountId);
                if (account == null) return Results.NotFound();

                account.DisplayName = req.DisplayName ?? account.DisplayName;
                account.ThemePreference = req.ThemePreference ?? account.ThemePreference;
                account.SignatureHtml = req.SignatureHtml ?? account.SignatureHtml;
                account.AutoResponderEnabled = req.AutoResponderEnabled ?? account.AutoResponderEnabled;
                account.AutoResponderMessage = req.AutoResponderMessage ?? account.AutoResponderMessage;
                account.RetentionPolicy = req.RetentionPolicy ?? account.RetentionPolicy;
                account.SovereigntyRegion = req.SovereigntyRegion ?? account.SovereigntyRegion;
                account.Require2FA = req.Require2FA ?? account.Require2FA;
                account.PopAccessEnabled = req.PopAccessEnabled ?? account.PopAccessEnabled;
                account.ImapAccessEnabled = req.ImapAccessEnabled ?? account.ImapAccessEnabled;

                await db.SaveChangesAsync();
                return Results.Ok();
            });

            api.MapPost("/block", async (int accountId, string emailOrDomain, AppDbContext db) =>
            {
                var account = await db.UserAccounts.FindAsync(accountId);
                if (account == null) return Results.NotFound();

                db.BlockedAddresses.Add(new BlockedAddress { UserAccountId = accountId, EmailAddressOrDomain = emailOrDomain });
                await db.SaveChangesAsync();
                return Results.Ok();
            });

            api.MapPost("/filters", async (int accountId, EmailFilterRule rule, AppDbContext db) =>
            {
                rule.UserAccountId = accountId;
                db.EmailFilterRules.Add(rule);
                await db.SaveChangesAsync();
                return Results.Ok();
            });

            api.MapGet("/storage", async (int accountId, AppDbContext db) =>
            {
                var account = await db.UserAccounts.FindAsync(accountId);
                if (account == null) return Results.NotFound();

                long totalStorageMb = 15360; // 15 GB
                long usedStorageMb = new Random().Next(400, 12000); 

                return Results.Ok(new {
                    TotalMb = totalStorageMb,
                    UsedMb = usedStorageMb,
                    Percentage = Math.Round((double)usedStorageMb / totalStorageMb * 100, 1)
                });
            });
        }
    }
}
