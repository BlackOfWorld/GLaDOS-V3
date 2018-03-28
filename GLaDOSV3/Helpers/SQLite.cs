﻿using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace GladosV3.Helpers
{
    public static class SqLite
    {
        public static string DirPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Database.db");
        public static SQLiteConnection Connection = new SQLiteConnection($"Data Source={DirPath}");
        /// <summary>
        ///  Returns a bool if a table exists.
        /// </summary>
        public static Task<bool> TableExistsAsync(this IDbConnection connection, string tableName)
        {
            object result;
            using (SQLiteCommand cmd = new SQLiteCommand(Connection))
            {
                cmd.CommandText = @"SELECT COUNT(*) FROM sqlite_master WHERE name=@TableName";
                var p1 = cmd.CreateParameter();
                p1.DbType = DbType.String;
                p1.ParameterName = "TableName";
                p1.Value = tableName;
                cmd.Parameters.Add(p1);
                result = cmd.ExecuteScalar();
            }

            return Task.FromResult(((long)result) == 1);
        }
        /// <summary>
        /// Creates a table with a name and parameters..
        /// </summary>
        public static Task CreateTableAsync(this SQLiteConnection connection, string tableName,string parameters)
        {
            string sql = $"CREATE TABLE `{tableName}` ({parameters});";
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
                command.ExecuteNonQuery();
            return Task.CompletedTask;
        }
        /// <summary>
        /// Sets/updates a value in a table.
        /// </summary>
        public static Task SetValueAsync<T>(this SQLiteConnection connection, string tableName, string parameter,T value, string guildid = "")
        {
            string sql = $"UPDATE {tableName} SET {parameter}='{value.ToString()}'";
            if (guildid != "")
                sql += $" WHERE guildid={guildid}";
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
                command.ExecuteNonQuery();
            return Task.CompletedTask;
        }
        /// <summary>
        /// Returns a DataTable.
        /// </summary>
        public static Task<DataTable> GetValuesAsync(this SQLiteConnection connection, string tableName,string guildid="")
        {
            string sql = $"SELECT * FROM {tableName}";
            DataTable dt = new DataTable();
            if (guildid != "")
                sql += $" WHERE guildid='{guildid}'";
            using (SQLiteDataAdapter reader = new SQLiteDataAdapter(sql, connection))
                reader.Fill(dt);
            dt.TableName = tableName;
            return Task.FromResult(dt);
        }
        /// <summary>
        /// Starts SQLite connection and checks for tables.
        /// </summary>
        public static void Start()
        {
            if (!File.Exists(DirPath))
                SQLiteConnection.CreateFile(DirPath);
            Connection.Open();
            if(!Connection.TableExistsAsync("servers").GetAwaiter().GetResult())
                Connection.CreateTableAsync("servers", "`guildid` INTEGER,`nsfw` INTEGER DEFAULT \'false\',`joinleave_cid` INTEGER,`join_msg` TEXT DEFAULT \'Hey {mention}! Welcome to {sname}!\',`join_toggle` INTEGER DEFAULT 0,`leave_msg` TEXT DEFAULT \'Bye {uname}!\',`leave_toggle` INTEGER DEFAULT 0");
            if (!Connection.TableExistsAsync("BlacklistedUsers").GetAwaiter().GetResult())
                Connection.CreateTableAsync("BlacklistedUsers", "`UserId` INTEGER,`Reason` TEXT DEFAULT \'Unspecified.\', `Date` INTEGER");
        }
        /// <summary>
        /// Adds a new row into a table.
        /// </summary>
        public static Task AddRecordAsync<T>(this SQLiteConnection connection, string tablename, string values, T[] items,string filter="")
        {
            string result = "";
            for (int i = 1; i <= items.Length; i++)
            {
                result += $"@val{i},";
            }
            string sql = $"INSERT INTO {tablename} ({values}) VALUES ({result.Remove(result.Length - 1)}) ";
            if (filter != "")
                sql += $"WHERE {filter}";
            using (SQLiteCommand command = new SQLiteCommand(sql, Connection))
            {
                for (int i = 1; i <= items.Length; i++)
                    command.Parameters.AddWithValue($"@val{i}", items[i - 1]);
                command.ExecuteNonQuery();
            }
            return Task.CompletedTask;
        }
        /// <summary>
        /// Removes a row from the selected table.
        /// </summary>
        public static Task RemoveRecordAsync(this SQLiteConnection connection, string tablename,string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return Task.FromException(new SQLiteException("Filter mustn't be empty!"));
            string sql = $"DELETE FROM {tablename} WHERE {filter}";
            using (SQLiteCommand command = new SQLiteCommand(sql, Connection))
                command.ExecuteNonQuery();
            return Task.CompletedTask;
        }
    }
}
