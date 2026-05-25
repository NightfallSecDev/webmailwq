using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WebmailClient.Data;
using WebmailClient.Models;
using System.Linq;
using System.Threading.Tasks;

namespace WebmailClient.Actions
{
    public static class ContactsActions
    {
        public static void MapContactsEndpoints(this IEndpointRouteBuilder routes)
        {
            var api = routes.MapGroup("/api/contacts");

            // List Contacts
            api.MapGet("/", async (int accountId, AppDbContext db) =>
            {
                var contacts = await db.Contacts
                    .Where(c => c.UserAccountId == accountId)
                    .OrderBy(c => c.Name)
                    .ToListAsync();
                return Results.Ok(contacts);
            });

            // Create Contact
            api.MapPost("/", async (int accountId, Contact contact, AppDbContext db) =>
            {
                var account = await db.UserAccounts.FindAsync(accountId);
                if (account == null) return Results.NotFound();

                contact.UserAccountId = accountId;
                db.Contacts.Add(contact);
                await db.SaveChangesAsync();

                return Results.Ok(contact);
            });

            // Update Contact
            api.MapPut("/{id}", async (int accountId, int id, Contact update, AppDbContext db) =>
            {
                var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Id == id && c.UserAccountId == accountId);
                if (contact == null) return Results.NotFound();

                contact.Name = update.Name ?? contact.Name;
                contact.Email = update.Email ?? contact.Email;
                contact.Phone = update.Phone ?? contact.Phone;
                contact.Company = update.Company ?? contact.Company;

                await db.SaveChangesAsync();
                return Results.Ok(contact);
            });

            // Delete Contact
            api.MapDelete("/{id}", async (int accountId, int id, AppDbContext db) =>
            {
                var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Id == id && c.UserAccountId == accountId);
                if (contact == null) return Results.NotFound();

                db.Contacts.Remove(contact);
                await db.SaveChangesAsync();
                return Results.Ok();
            });
        }
    }
}
