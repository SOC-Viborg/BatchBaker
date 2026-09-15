using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BatchBaker
{
    internal static class MainWindowHelpers
    {
        public static IEnumerable<Person> ReadCSV(string filePath)
        {
            try
            {
                // Use the provided file path directly (already has .csv extension)
                string fullPath = filePath.EndsWith(".csv") ? filePath : Path.ChangeExtension(filePath, ".csv");

                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"CSV file not found: {fullPath}");
                }

                string[] lines = File.ReadAllLines(fullPath);

                // Skip header row and project each line as a Person
                return lines.Skip(1).Select(line =>
                {
                    string[] data = line.Split(',');
                    if (data.Length < 9)
                    {
                        throw new FormatException($"CSV line does not contain expected 9 fields: {line}");
                    }

                    // Parse phone number, trimming whitespace and removing formatting characters
                    string phoneStr = data[7].Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
                    if (!int.TryParse(phoneStr, out int phoneNumber))
                    {
                        // If it's not parseable as int, store 0 or try alternative approaches
                        phoneNumber = 0;
                    }

                    // Use the full constructor with all 9 fields, trimming whitespace from all fields
                    return new Person(
                        data[0].Trim(), 
                        data[1].Trim(), 
                        data[2].Trim(), 
                        data[3].Trim(), 
                        data[4].Trim(), 
                        data[5].Trim(), 
                        data[6].Trim(), 
                        phoneNumber, 
                        data[8].Trim()
                    );
                }).ToList(); // Materialize the list to ensure all parsing happens before returning
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException($"Error parsing CSV file: {ex.Message}");
            }
        }

        // Danish display labels for UI
        public static readonly Dictionary<string, string> DanishLabels = new()
        {
            { nameof(Person.FirstName), "Fornavn" },
            { nameof(Person.LastName), "Efternavn" },
            { nameof(Person.Username), "Brugernavn" },
            { nameof(Person.Email), "Email" },
            { nameof(Person.Department), "Afdeling" },
            { nameof(Person.Country), "Land" },
            { nameof(Person.Title), "Titel" },
            { nameof(Person.PhoneNumber), "Telefonnummer" },
            { nameof(Person.TemporaryPassword), "Midlertidig adgangskode" }
        };
    }

    public class Person
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Department { get; set; }
        public string Country { get; set; }
        public string Title { get; set; }
        public int PhoneNumber { get; set; }
        public string TemporaryPassword { get; set; }

        public Person()
        {
        }

        public Person(string firstName, string lastName, string username, string email, string department, string country, string title, int phoneNumber, string password)
        {
            FirstName = firstName;
            LastName = lastName;
            Username = username;
            Email = email;
            Department = department;
            Country = country;
            Title = title;
            PhoneNumber = phoneNumber;
            TemporaryPassword = password;
        }
    }       
}
