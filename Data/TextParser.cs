using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WFM.Data
{
    public class TextParser
    {
        /// <summary>
        /// Checks the Cache Set for a table and the column matching the command. (ex: %Table.Column%)
        /// </summary>
        public static bool IsTableCommand(string command, Cache shared_data)
        {
            DataTable table = null;

            // For this string to be a table command it must contain exactly two '%' that begin and end the string and it must also contain a '.'. 
            if(command.StartsWith("%") && command.EndsWith("%") && command.Contains(".") && command.Split('%').GetUpperBound(0) == 2)
            {
                // The text between the starting '%' and the '.' must resolve to the name of a table in the cache.
                if(shared_data.Data.Contains(command.Substring(0, command.IndexOf(".")).Replace("%","")))
                {                    
                    table = shared_data.Data.Tables(command.Substring(0, command.IndexOf(".")).Replace("%", ""));

                    // The text between the '.' and the ending '%' must resolved to the name of the column in the referenced global table.
                    if (table.Columns.Contains(command.Substring(command.IndexOf(".") + 1).Replace("%", "")))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static DataTable GetCommandTable(string command, Cache shared_data)
        {
            if(shared_data.Data.Contains(command.Substring(0, command.IndexOf(".")).Replace("%", "")))
            {
                return shared_data.Data.Tables(command.Substring(0, command.IndexOf(".")).Replace("%", ""));
            }

            return null;
        }

        public static string GetCommandColumnName(string command)
        {
            return command.Substring(command.IndexOf(".") + 1).Replace("%", "");
        }

        public static object GetCommandColumnValue(string command, DataRow row)
        {
            return row[command.Substring(command.IndexOf(".") + 1).Replace("%", "")];
        }

        public static List<string> Parse(string command_name, Cache shared_data, List<ArrayCommand> commands)
        {
            List<string> collection = null;
            DataTable data_table    = null;            

            if(commands != null)
            {
                foreach(ArrayCommand command in commands)
                {
                    if (command.Name == command_name)
                        return command.Value;
                }
            }

            if(IsTableCommand(command_name, shared_data))
            {
                data_table = GetCommandTable(command_name, shared_data);

                collection = new List<string>();

                foreach(DataRow row in data_table.Rows)
                {
                    collection.Add(TextParser.Parse(command_name, row, shared_data, null));
                }
            }

            return collection;
        }

        public static string Parse(string command_name, List<StringCommand> commands)
        {
            foreach(StringCommand command in commands)
            {
                if (command.Name.ToUpper() == command_name.ToUpper())
                    return command.Value;
            }

            return null;
        }

        public static string Parse(string text, DataRow row, Cache shared_data, List<StringCommand> commands)
        {
            string result = "";
            int start = 0;
            List<string> chunks = null;

            if(!string.IsNullOrEmpty(text))
            {
                // Check the text for the filter key.
                if (text.Contains("%"))
                {
                    chunks = new List<string>();

                    // Separate normal text and commands from the string and store them into chunks.
                    for (int end = 0; end <= text.Length - 1; end++)
                    {
                        if (text[end] == '%')
                        {
                            if (start != end)
                            {
                                if (text[start] == '%')
                                {
                                    chunks.Add(text.Substring(start, end - (start - 1)));
                                    start = end + 1;
                                }
                                else
                                {
                                    chunks.Add(text.Substring(start, end - start));
                                    start = end;
                                }
                            }
                        }
                        else if (end == text.Length - 1)
                        {
                            chunks.Add(text.Substring(start, end - (start - 1)));
                        }
                    }

                    foreach (string chunk in chunks)
                    {
                        if (!string.IsNullOrEmpty(chunk))
                        {
                            if (chunk.StartsWith("%") && chunk.EndsWith("%"))
                            {
                                result += ProcessCommand(chunk, row, shared_data, commands);
                            }
                            else
                            {
                                result += chunk;
                            }
                        }
                    }
                }
                else
                {
                    result = text;
                }
            }

            return result;
        }

        public static List<string> ParseStringArray(string command, Cache shared_data)
        {
            List<string> collection = null;
            DataTable table = null;
                        
            table = GetCommandTable(command, shared_data);

            command = command.Replace("%", "");

            if(table.Columns.Contains(command.Substring(command.IndexOf("."))))
            {
                collection = new List<string>();

                foreach(DataRow row in table.Rows)
                {
                    collection.Add(row[command.Substring(command.IndexOf("."))].ToString());
                }
            }

            return collection;
        }

        private static string ProcessCommand(string command, DataRow row, Cache shared_data, List<StringCommand> commands)
        {
            string timespan_amount = null;
            string table_name = null;
            string column_name = null;
			string concat_type = null;
            string format = null;
            string result = null;

            command = command.Replace("%", "");

            if(command.Contains("@"))
            {
                timespan_amount = command.Substring(command.IndexOf("@"));
                command         = command.Replace(timespan_amount, "");
                timespan_amount = timespan_amount.Replace("@", "");
            }

            if(command.Contains("|"))
            {
                format  = command.Substring(command.IndexOf("|"));
                command = command.Replace(format, "");
                format  = format.Replace("|", "");
            }

            if(command.Contains("."))
            {
                table_name  = command.Substring(0, command.IndexOf("."));
                column_name = command.Substring(command.IndexOf(".") + 1);

				if (column_name.Contains("[',']"))
				{
					column_name = column_name.Replace("[',']", "");
					concat_type = "string";
				}
				else if (column_name.Contains("[,]"))
				{
					column_name = column_name.Replace("[,]", "");
					concat_type = "int";
				}

				if (row != null && table_name == row.Table.TableName)
                {
                    if (row.Table.Columns.Contains(column_name))
                    {
                        if (row.Table.Columns[column_name].DataType == typeof(DateTime))
                        {
                            if(!Convert.IsDBNull(row[column_name]))
                            {
                                result = Convert.ToDateTime(row[column_name]).ToString(format);
                            }
                            else
                            {
                                result = "";
                            }
                        }
                        else
                        {
                            result = row[column_name].ToString();
                        }
                    }
                    else
                    {
                        throw new Exception("The referenced column '" + column_name + "' in the command '" + command + "' does not exist in the referenced data table.");
                    }
                }
                else if(shared_data.Data.Contains(table_name))
                {
					if (shared_data.Data.Tables(table_name).Rows.Count == 1)
                    {
                        if (shared_data.Data.Tables(table_name).Columns.Contains(column_name))
                        {
							if (!string.IsNullOrEmpty(concat_type))
							{
								var concat = concat_type == "string" ? "'" : "";

								if (shared_data.Data.Tables(table_name).Rows.Count > 0)
								{
									foreach (DataRow r in shared_data.Data.Tables(table_name).Rows)
									{
										result += concat + r[column_name].ToString() + concat + ",";
									}
								}

								// Remove the last comma from the string.
								result = result.Remove(result.Length - 1);
							}
							else if (shared_data.Data.Tables(table_name).Columns[column_name].DataType == typeof(DateTime))
                            {
                                if (!Convert.IsDBNull(shared_data.Data.Tables(table_name).Rows[0][column_name]))
                                {
                                    if (!string.IsNullOrEmpty(timespan_amount))
                                        result = Convert.ToDateTime(shared_data.Data.Tables(table_name).Rows[0][column_name]).Add(ApplyTimeSpan(timespan_amount.Split('.'))).ToString(format);
                                    else
                                        result = Convert.ToDateTime(shared_data.Data.Tables(table_name).Rows[0][column_name]).ToString(format);
                                }
                                else
                                    result = "";
                            }
                            else
                            {
                                result = shared_data.Data.Tables(table_name).Rows[0][column_name].ToString();
                            }
                        }
                        else if (column_name == "__ROWS_COUNT")
                            result = "1";
                        else
                            throw new Exception("The referenced column in the command %" + command + "% does not exist in the referenced data table '" + table_name + "'");
                    }
                    else if (column_name == "__ROWS_COUNT")
                    {
                        if (shared_data.Data.Tables(table_name).Rows.Count > 0)
                        {
                            result = shared_data.Data.Tables(table_name).Rows.Count.ToString();
                        }
                        else
                            result = "0";
                    }
                    else
                    {
						if (!string.IsNullOrEmpty(concat_type))
						{
							var concat = concat_type == "string" ? "'" : "";

							foreach (DataRow r in shared_data.Data.Tables(table_name).Rows)
							{
								result += concat + r[column_name].ToString() + concat + ",";
							}

							// Remove the last comma from the string.
							if (!string.IsNullOrEmpty(result))
							{
								result = result.Remove(result.Length - 1);
							}
							else
							{
								result = "''";
							}
						}
						else
						{
							throw new Exception("The command %" + command + "% is referencing a table with multiple rows. Unable to proceed.");
						}
                    }
                }
                else
                {
                    throw new Exception("The referenced table in the command %" + command + "% does not exist in the global cache.");
                }
            }
            else
            {
                if (command == "DateTimeNow")
                {
                    result = DateTime.Now.ToString(format);
                }
                else
                {
                    foreach (StringCommand cmd in commands)
                    {
                        if (command == cmd.Name.Replace("%", ""))
                        {
                            result = cmd.Value;
                            break;
                        }
                    }
                }
            }

            return result;
        }

        public static List<StringCommand> Concat(List<StringCommand> one, List<StringCommand> two)
        {
            if (one == null)
                one = new List<StringCommand>();

            if(two != null)
                one.AddRange(two);

            return one;
        }

        public static List<ArrayCommand> Concat(List<ArrayCommand> one, List<ArrayCommand> two)
        {
            one.AddRange(two);
            return one;
        }

        public static InternalCommandType CommandType(string command)
        {
            if(command.StartsWith("%") && command.EndsWith("%"))
            {
                if (command.Contains("."))
                    return InternalCommandType.DynamicCommand;
                else
                    return InternalCommandType.StaticCommand;
            }

            return InternalCommandType.None;
        }

        protected static TimeSpan ApplyTimeSpan(string[] time_array)
        {
            if(time_array.Length == 5)
            {
                return new TimeSpan(Convert.ToInt32(time_array[0]), Convert.ToInt32(time_array[1]), Convert.ToInt32(time_array[2]), Convert.ToInt32(time_array[3]), Convert.ToInt32(time_array[4]));
            }

            throw new Exception("Command timespan is not in correct format.");
        }
    }

    public struct ArrayCommand
    {
        public string Name;
        public List<string> Value;
    }

    public class StringCommand
    {
        public string Name;
        public string Value;
    }

    public enum InternalCommandType
    {
        None = 0,
        StaticCommand = 1,
        DynamicCommand = 2
    }
}
