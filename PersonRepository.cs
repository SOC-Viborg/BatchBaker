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

        public List<Person> LoadAll()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT Id, FirstName, LastName, Username, Email, Department, Country, Title, PhoneNumber, TemporaryPassword
                FROM People
                ORDER BY Id;
                """;

            var people = new List<Person>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                people.Add(new Person
                {
                    Id = reader.GetInt32(0),
                    FirstName = reader.GetString(1),
                    LastName = reader.GetString(2),
                    Username = reader.GetString(3),
                    Email = reader.GetString(4),
                    Department = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Country = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Title = reader.IsDBNull(7) ? null : reader.GetString(7),
                    PhoneNumber = reader.GetInt32(8),
                    TemporaryPassword = reader.IsDBNull(9) ? null : reader.GetString(9)
                });
            }

            return people;
        }

        public int Delete(IEnumerable<int> ids)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM People WHERE Id = $id;";
            var idParameter = command.CreateParameter();
            idParameter.ParameterName = "$id";
            command.Parameters.Add(idParameter);

            int count = 0;
            foreach (var id in ids)
            {
                idParameter.Value = id;
                count += command.ExecuteNonQuery();
            }

            transaction.Commit();
            return count;
        }
    }
}