using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace BatchBaker
{
    internal sealed class PersonRepository
    {
        private readonly string _connectionString;

        public PersonRepository(string? databasePath = null)
        {
            string path = databasePath ?? Path.Combine(AppContext.BaseDirectory, "batchbaker.db");
            _connectionString = $"Data Source={path}";
        }

        public void EnsureCreated()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE IF NOT EXISTS People (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FirstName TEXT NOT NULL,
                    LastName TEXT NOT NULL,
                    Username TEXT NOT NULL,
                    Email TEXT NOT NULL,
                    Department TEXT,
                    Country TEXT,
                    Title TEXT,
                    PhoneNumber INTEGER,
                    TemporaryPassword TEXT
                );
                """;
            command.ExecuteNonQuery();
        }

        public int SaveAll(IEnumerable<Person> people)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO People (FirstName, LastName, Username, Email, Department, Country, Title, PhoneNumber, TemporaryPassword)
                VALUES ($firstName, $lastName, $username, $email, $department, $country, $title, $phoneNumber, $temporaryPassword);
                """;

            int count = 0;
            foreach (var person in people)
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$firstName", person.FirstName ?? string.Empty);
                command.Parameters.AddWithValue("$lastName", person.LastName ?? string.Empty);
                command.Parameters.AddWithValue("$username", person.Username ?? string.Empty);
                command.Parameters.AddWithValue("$email", person.Email ?? string.Empty);
                command.Parameters.AddWithValue("$department", person.Department ?? string.Empty);
                command.Parameters.AddWithValue("$country", person.Country ?? string.Empty);
                command.Parameters.AddWithValue("$title", person.Title ?? string.Empty);
                command.Parameters.AddWithValue("$phoneNumber", person.PhoneNumber);
                command.Parameters.AddWithValue("$temporaryPassword", person.TemporaryPassword ?? string.Empty);
                command.ExecuteNonQuery();
                count++;
            }

            transaction.Commit();
            return count;
        }
    }
}