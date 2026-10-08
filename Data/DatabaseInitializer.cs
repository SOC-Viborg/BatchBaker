using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BatchBaker.Data
{
    // EnsureCreated() only builds a database that doesn't exist yet, so a database
    // made by an older version never gets tables or columns added later. This adds
    // whatever is missing at startup. It never drops or alters existing columns, so
    // a renamed or removed property just leaves its old column behind.
    public static class DatabaseInitializer
    {
        public static void EnsureSchema(AppDbContext db)
        {
            if (db.Database.EnsureCreated())
            {
                return; // brand-new database, already complete
            }

            DbConnection connection = db.Database.GetDbConnection();
            bool openedHere = connection.State != ConnectionState.Open;
            if (openedHere)
            {
                connection.Open();
            }

            try
            {
                string createScript = db.Database.GenerateCreateScript();

                foreach (ITable table in db.Model.GetRelationalModel().Tables)
                {
                    var existingColumns = GetExistingColumns(connection, table.Name);
                    if (existingColumns.Count == 0)
                    {
                        CreateTable(connection, createScript, table.Name);
                        continue;
                    }

                    foreach (IColumn column in table.Columns)
                    {
                        if (!existingColumns.Contains(column.Name))
                        {
                            Execute(connection, BuildAddColumn(table.Name, column));
                        }
                    }
                }
            }
            finally
            {
                if (openedHere)
                {
                    connection.Close();
                }
            }
        }

        private static HashSet<string> GetExistingColumns(DbConnection connection, string table)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{table}\");";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                columns.Add(reader.GetString(reader.GetOrdinal("name")));
            }
            return columns;
        }

        // Reuses EF's own CREATE TABLE (and index) statements for the missing table.
        private static void CreateTable(DbConnection connection, string createScript, string table)
        {
            var statements = createScript
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.StartsWith($"CREATE TABLE \"{table}\"", StringComparison.Ordinal)
                         || (s.StartsWith("CREATE", StringComparison.Ordinal) && s.Contains($" ON \"{table}\"", StringComparison.Ordinal)));

            foreach (string statement in statements)
            {
                Execute(connection, statement + ";");
            }
        }

        // SQLite only accepts a new NOT NULL column if it has a default for existing rows.
        private static string BuildAddColumn(string table, IColumn column)
        {
            string sql = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column.Name}\" {column.StoreType}";
            if (!column.IsNullable)
            {
                sql += $" NOT NULL DEFAULT {DefaultValueFor(column)}";
            }
            return sql + ";";
        }

        private static string DefaultValueFor(IColumn column)
        {
            Type clrType = Nullable.GetUnderlyingType(column.ProviderClrType) ?? column.ProviderClrType;
            return column.StoreType.ToUpperInvariant() switch
            {
                "TEXT" when clrType == typeof(DateTime) => "'0001-01-01 00:00:00'",
                "TEXT" => "''",
                "BLOB" => "X''",
                _ => "0" // INTEGER, REAL, NUMERIC (bool, int, decimal, ...)
            };
        }

        private static void Execute(DbConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }
}
