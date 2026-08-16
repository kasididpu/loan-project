using LoanProject.Domain.Customers;
using LoanProject.Infrastructure.Persistence.Repositories;
using Microsoft.Data.SqlClient;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// End-to-end proof that field-level encryption is wired through EF: the value is
/// ciphertext in the column but decrypts transparently on read. Integration test
/// against the real dev SQL Server (docker compose must be up).
/// </summary>
public class CustomerPiiEncryptionTests
{
    [Fact]
    public async Task NationalId_IsCiphertextAtRest_ButDecryptsOnRead()
    {
        var id = Guid.NewGuid();
        const string nationalId = "1234512345123";

        await using (var db = TestDatabase.CreateContext())
        {
            var customer = new Customer(id, "Encryption Test", DateTime.UtcNow);
            customer.SetIdentityDocuments(nationalId, "999-9-99999-9");
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
        }

        // Raw column read bypasses EF's converter — it must NOT be the plaintext.
        await using (var connection = new SqlConnection(TestDatabase.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand("SELECT NationalId FROM Customer WHERE Id = @id", connection);
            command.Parameters.AddWithValue("@id", id);

            var stored = (string?)await command.ExecuteScalarAsync();
            Assert.NotNull(stored);
            Assert.NotEqual(nationalId, stored);
        }

        // EF read applies the converter → the original value is restored.
        await using (var db = TestDatabase.CreateContext())
        {
            var customer = await new CustomerRepository(db).FindAsync(id, CancellationToken.None);
            Assert.Equal(nationalId, customer!.NationalId);
        }
    }
}
