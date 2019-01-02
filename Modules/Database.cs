using Schalltech.EnterpriseLibrary.Configuration.SqlServer;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using WFM.Data;

namespace WFM.Modules
{
    public class Database : BaseModule
    {
        [XmlElement(ElementName = "Commands")]
        public DatabaseCommandCollection DatabaseCommandCollection { get; set; }

		[XmlElement(ElementName = "DrivingModule")]
		public CacheTable DrivingModule { get; set; }

		protected List<List<DatabaseCommand>> DatabaseCommandBatch = null;

        public Database()
        {}

        public Database(Cache shared_data, Database configuration) 
            : base(shared_data, configuration)
        {
            DatabaseCommandCollection = configuration.DatabaseCommandCollection;

			if (configuration.DrivingModule != null)
				DrivingModule = new CacheTable(SharedData, DrivingData, configuration.DrivingModule);
		}

        public void Init()
        {
            for(int i = 0; i < DatabaseCommandCollection.Count; i++)
            {
                try
                {
                    DatabaseCommandCollection.Items[i] = new DatabaseCommand(i + 1, this, DatabaseCommandCollection.Items[i], SharedData, null, ModuleCommands, DrivingData, -1);
                }
                catch (Exception ex)
                {
                    Logging.Logger.Aquire();
                    Logging.Logger.WriteLine("Database.Init", "", System.Diagnostics.TraceEventType.Information, 2, i, SharedData.LogCategory);
                    Logging.Logger.WriteLine("Database.Init", "BatchProcess :" + ex.Message, System.Diagnostics.TraceEventType.Information, 2, i, SharedData.LogCategory);
                }
                finally
                {
                    Logging.Logger.Release();
                }
            }
        }

        private void SafeExecute(Action method, out Exception e)
        {
            try
            {
                e = null;
                method.Invoke();
            }
            catch (Exception ex)
            {
                e = ex;
                //StopThreads = true;
                //ExitProcess = true;
            }
        }

        private void BatchProcess()
        {
            Thread[] execution_threads = null;
            DatabaseCommand database_command = null;
            Exception threadEx = null;

            try
            {
                foreach (List<DatabaseCommand> command_group in DatabaseCommandBatch)
                {
                    execution_threads = new Thread[command_group.Count];

                    for (int i = 0; i <= execution_threads.GetUpperBound(0); i++)
                    {
                        try
                        {
                            database_command = new DatabaseCommand(i + 1, this, command_group[i], SharedData, null, ModuleCommands, DrivingData, -1);

                            execution_threads[i] = new Thread(() => SafeExecute(() => database_command.Execute(), out threadEx));
                            execution_threads[i].Start();
                            command_group[i] = database_command;

                            // Check for any errors on the thread.
                            if (threadEx != null)
                            {
                                throw threadEx;
                            }
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                Logging.Logger.Aquire();
                                Logging.Logger.Write("Database.BatchProcess", "", System.Diagnostics.TraceEventType.Information, 0, i, SharedData.LogCategory);
                                Logging.Logger.Write("Database.BatchProcess", "BatchProcess :" + ex.Message, System.Diagnostics.TraceEventType.Information, 0, i, SharedData.LogCategory);
                            }
                            catch (Exception e)
                            {
                                throw e;
                            }
                            finally
                            {
                                Logging.Logger.Release();
                            }
                        }
                    }

                    for (int i = 0; i <= execution_threads.GetUpperBound(0); i++)
                    {
                        try
                        {
                            Logging.Logger.Aquire();
                            Logging.Logger.Write("Database.BatchProcess", "", System.Diagnostics.TraceEventType.Information, 0, 0, SharedData.LogCategory);
                            Logging.Logger.Write("Database.BatchProcess", "     DATABASE MODULE: " + String.Format("Waiting for thread [{0}] to complete", i + 1), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                        finally
                        {
                            Logging.Logger.Release();
                        }

                        // Wait for all the commands to complete.
                        execution_threads[i].Join();

                        try
                        {
                            Logging.Logger.Aquire();
                            Logging.Logger.Write("Database.BatchProcess", "", System.Diagnostics.TraceEventType.Information, 0, 0, SharedData.LogCategory);
                            Logging.Logger.Write("Database.BatchProcess", "     DATABASE MODULE: " + String.Format("Thread [{0}] has completed", i + 1), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                        finally
                        {
                            Logging.Logger.Release();
                        }

                        // Check for any errors during executing the database commands.
                        if (threadEx != null)
                        {
                            throw threadEx;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("{0} Module '{1}' -> {2}", GetType().Name, Name, ex.Message), ex);
            }
            finally
            {
                execution_threads = null;
            }
        }

        protected override void OnProcess(object sender, EventArgs e)
        {
			System.Data.DataTable DrivingTable;

			try 
	        {
				//if (DrivingModule != null)
				//{
				//	//Get the driving module from the shared data.
				//	if (SharedData.Data.Contains(DrivingModule.Name))
				//	{
				//		DrivingTable = DrivingModule.Process();

				//		foreach (System.Data.DataRow row in DrivingTable.Rows)
				//		{
				//			DrivingData = row;

				//			InnerProcess();
				//		}
				//	}
				//	else
				//		throw new Exception(string.Format("The referenced source table {0} does not exist in the global cache set.", DrivingModule.Name));
				//}
				//else
				//{
					InnerProcess();
				//}
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

		private void InnerProcess()
		{
			int current_run_index;
			DatabaseCommand uProc, sProc;
			List<DatabaseCommand> command_group = null;

			try
			{
				if (DatabaseCommandCollection.Count > 0)
				{
					// Sort the database commands by their run index.
					for (int sIndex = 0; sIndex < DatabaseCommandCollection.Count; sIndex++)
					{
						for (int uIndex = (sIndex + 1); uIndex < DatabaseCommandCollection.Count; uIndex++)
						{
							sProc = DatabaseCommandCollection.Items[sIndex];
							uProc = DatabaseCommandCollection.Items[uIndex];

							if (sProc.RunIndex > uProc.RunIndex)
							{
								DatabaseCommandCollection.Items[sIndex] = uProc;
								DatabaseCommandCollection.Items[uIndex] = sProc;
							}

							sProc = null;
							uProc = null;
						}
					}

					// Split the commands into groups based on thier RunIndex.
					// Add the groups to a stored procedure batch list for execution.
					current_run_index = 1;
					foreach (DatabaseCommand command in DatabaseCommandCollection.Items)
					{
						if (current_run_index != command.RunIndex && command_group != null)
						{
							// The CurrentRunIndex is different than the current database command group's RunIndex.
							// Add the database command group to the batch.
							if (DatabaseCommandBatch == null)
								DatabaseCommandBatch = new List<List<DatabaseCommand>>();

							DatabaseCommandBatch.Add(command_group);

							command_group = null;

							current_run_index = command.RunIndex;
						}

						if (command_group == null)
							command_group = new List<DatabaseCommand>();

						// Add the command to the group.
						command_group.Add(command);
					}

					// Add the last group to the batch.
					if (command_group != null)
					{
						if (DatabaseCommandBatch == null)
							DatabaseCommandBatch = new List<List<DatabaseCommand>>();

						DatabaseCommandBatch.Add(command_group);
						command_group = null;
					}

					BatchProcess();
				}
			}
			catch (Exception ex)
			{
				throw ex;
			}
		}
    }

    public class DatabaseCommandCollection
    {
        private List<DatabaseCommand> items;

        [XmlElement(ElementName = "Command")]
        public List<DatabaseCommand> Items
        {
            get
            {
                if (items == null)
                    items = new List<DatabaseCommand>();

                return items;
            }
            set
            {
                items = value;
            }
        }

        public int Count
        {
            get
            {
                if (Items != null)
                    return Items.Count;

                return 0;
            }
        }
    }

    public class DatabaseCommand
    {
        #region Variables

        private int maxAttempts = 3;
        private string enabled = "true";

        #endregion

        #region Properties

        [XmlAttribute(AttributeName = "RunIndex")]
        public int RunIndex { get; set; }

        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "Type")]
        public string CommandType { get; set; }

        [XmlAttribute(AttributeName = "Text")]
        public string CommandText { get; set; }

        [XmlAttribute(AttributeName = "MaxAttempts")]
        public int MaxAttempts
        {
            get
            {
                return maxAttempts;
            }
            set
            {
                maxAttempts = value;
            }
        }

		[XmlAttribute(AttributeName = "GoToModuleOnNoData")]
		public string GoToModuleOnNoData { get; set; }

		[XmlAttribute(AttributeName = "Enabled")]
        public string Enabled
        {
            get
            {
                return enabled;
            }
            set
            {
                enabled = value;
            }
        }

        [XmlElement(ElementName = "DrivingModule")]
		public CacheTable DrivingModule { get; set; }

		[XmlElement(ElementName = "Connection")]
        public DatabaseConnection Connection { get; set; }

        [XmlElement(ElementName = "Parameters")]
        public ParameterCollection ParameterCollection { get; set; }

        [XmlElement(ElementName = "Reader")]
        public string Reader { get; set; }

        [XmlIgnore]
        public Exception LastError { get; set; }

        protected List<ArrayCommand> ArrayCommands { get; set; }
        protected List<StringCommand> StringCommands { get; set; }
        protected int LastArrayElementNumber { get; set; }
        protected int ThreadID { get; set; }
        protected object DBCommand { get; set; }
        protected Cache SharedData { get; set; }
        protected DataRow DrivingData { get; set; }
        protected DateTime ExecuteStart { get; set; }
        public bool OnlyInputParams
        {
            get
            {
                if(ParameterCollection != null && ParameterCollection.Count > 0)
                {
                    foreach(Parameter param in ParameterCollection.Items)
                        if (param.Direction.ToLower() == "output")
                            return false;
                }

                return true;
            }
        }
        public bool OnlyStandardOutputExists
        {
            get
            {   
                bool standard = false;

                if (ParameterCollection != null && ParameterCollection.Count > 0)
                {
                    foreach (Parameter param in ParameterCollection.Items)
                    {
                        if (param.Direction.ToLower() == "output")
                        {
                            standard = true;

                            if (param.DataType == "refcursor")
                                return false;
                        }
                    }       
                }

                return standard;
            }
        }

        public string ConnectionString
        {
            get
            {
                return Schalltech.EnterpriseLibrary.Configuration.ConfigurationManager.GetConfiguration<SqlServerConfiguration>(TextParser.Parse(Connection.String, DrivingData, SharedData, StringCommands)).ConnectionString;
            }
        }

        public List<string> CursorDataSetTableNames
        {
            get
            {
                List<string> table_names = null;

                if(ParameterCollection != null)
                {
                    foreach(Parameter param in ParameterCollection.Items)
                    {
                        if(param.DataType == "REFCURSOR" && !string.IsNullOrEmpty(param.DataSetTableName))
                        {
                            if (table_names == null)
                                table_names = new List<string>();

                            table_names.Add(param.DataSetTableName);
                        }
                    }
                }

                return table_names;
            }
        }

        public List<string> AllDataSetTableNames
        {
            get
            {
                List<string> table_names = new List<string>();
                List<string> cursor_table_names = null;

                if(!string.IsNullOrEmpty(Name))
                {
                    //if(ParameterCollection != null && ParameterCollection.Count > 0)
                    //{
                    //    foreach(Parameter parameter in ParameterCollection.Items)
                    //    {
                    //        if(parameter.Direction.ToLower() == "output")
                    //        {
                    //            table_names.Add(Name);
                    //            break;
                    //        }
                    //    }
                    //}
                    //else if(CommandType.ToLower() == "commandtext" || CommandType.ToLower() == "text")
                    //{
                    //    table_names.Add(Name);
                    //}

                    table_names.Add(Name);
                }

                if (!string.IsNullOrEmpty(Reader))
                    table_names.Add(Reader);

                // Applicable for Oracle only.
                cursor_table_names = CursorDataSetTableNames;
                if(cursor_table_names != null && cursor_table_names.Count > 0)
                {
                    foreach(string table_name in cursor_table_names)
                    {
                        if (table_names == null)
                            table_names = new List<string>();

                        table_names.Add(table_name);
                    }
                }

                return table_names;
            }
        }

		private BaseModule Parent { get; set; }
        #endregion

        public DatabaseCommand()
        { }

        public DatabaseCommand(int thread_index, BaseModule parent, DatabaseCommand configuration, Cache shared_data, List<ArrayCommand> array_commands, List<StringCommand> string_commands, DataRow driving_data_row, int last_populated_element)
        {
            ThreadID            = thread_index;
            RunIndex            = configuration.RunIndex;
            Name                = configuration.Name;
            CommandType         = configuration.CommandType;
            //CommandText       = BaseModule.decode(configuration.CommandText);
            CommandText         = configuration.CommandText;
            MaxAttempts         = configuration.MaxAttempts;
            Enabled             = configuration.Enabled;
            Connection          = configuration.Connection;
            Reader              = configuration.Reader;
			GoToModuleOnNoData  = configuration.GoToModuleOnNoData;
			Parent				= parent;

			ParameterCollection = configuration.ParameterCollection;

            SharedData = shared_data;
            DrivingData = driving_data_row;
            ArrayCommands = array_commands;

            StringCommands = TextParser.Concat(string_commands, GetStringCommands());

            LastArrayElementNumber = last_populated_element;

			if (configuration.DrivingModule != null)
				DrivingModule = new CacheTable(SharedData, DrivingData, configuration.DrivingModule);
		}

        public void Execute(bool generate_output_table = true)
        {
			System.Data.DataTable DrivingTable;

			// Verify the command is enabled.
			if (TextParser.Parse(Enabled, DrivingData, SharedData, null) == Boolean.TrueString.ToLower())
            {
                // Add the database command's output parameter and refcursor tables to the global cache set.
                if (generate_output_table)
                {
                    AddDatabaseCommandTables();
                }

				if (DrivingModule != null)
				{
					if (SharedData.Data.Contains(DrivingModule.Name))
					{
						DrivingTable = DrivingModule.Process();

						foreach (System.Data.DataRow row in DrivingTable.Rows)
						{
							DrivingData = row;

							switch (Connection.Type.ToUpper())
                            {
                                case "ORACLE":
                                    break;
                                case "DB2":
                                    break;
                                case "MSSQL":
                                    ExecuteMSSQLCommand();
                                    break;
                            }
                        }
                    }
                    else
						throw new Exception(string.Format("The referenced source table {0} does not exist in the global cache set.", DrivingModule.Name));
				}
                else
                {
                    switch (Connection.Type.ToUpper())
                    {
                        case "ORACLE":
                            break;
                        case "DB2":
                            break;
                        case "MSSQL":
                            ExecuteMSSQLCommand();
                            break;
                    }
                }
            }
            else
            {
                try
                {
                    Logging.Logger.Aquire();
                    Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "", System.Diagnostics.TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                    Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "             COMMAND: " + CommandText, System.Diagnostics.TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                    Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "             ENABLED: " + Enabled, System.Diagnostics.TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                    Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "              THREAD: " + ThreadID, System.Diagnostics.TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    Logging.Logger.Release();
                }
            }
        }

        protected void ExecuteMSSQLCommand()
        {
            SqlConnection conn = null;
            SqlCommand command = null;
            int attempts       = 1;
            string details     = null;
            
            while(true)
            {
                try
                {
					//var str = "Server=aws-dev1-sql.nobrainer2.com; Database=qa1; Integrated Security=True";

					using(conn = new SqlConnection(ConnectionString))
					//using (conn = new SqlConnection(str))
					{
                        OpenConnection(conn, null, null);

                        using(command = new SqlCommand())
                        {
                            command.Connection     = conn;
                            command.CommandText    = TextParser.Parse(CommandText, DrivingData, SharedData, StringCommands);                            
                            command.CommandTimeout = 0;

                            switch (CommandType.ToLower())
                            {
                                case "storedprocedure":
                                    command.CommandType = System.Data.CommandType.StoredProcedure;
                                    break;
                                case "text":
                                    command.CommandType = System.Data.CommandType.Text;
                                    break;
                                default:
                                    throw new Exception("The command type '" + CommandType + "' is not supported.");
                            }

                            details = BuildSqlServerParameters(command);

                            try
                            {
                                Logging.Logger.Aquire();
                                Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "", System.Diagnostics.TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                                Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "   EXECUTING COMMAND: " + command.CommandText, System.Diagnostics.TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                                Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "          PARAMETERS: " + details.Trim(), System.Diagnostics.TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                                Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "             ATTEMPT: " + attempts + "/" + MaxAttempts, System.Diagnostics.TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                                Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "              THREAD: " + ThreadID, System.Diagnostics.TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                                //Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "          CONNECTION: " + conn.ConnectionString, System.Diagnostics.TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);

                                if(LastError != null)
                                {
                                    Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "      LAST EXCPN MSG: " + LastError.Message, System.Diagnostics.TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                                    LastError = null;
                                }
                            }
                            catch (Exception ex)
                            {
                                throw ex;
                            }
                            finally
                            {
                                Logging.Logger.Release();
                            }

                            // Capture when the execution of the command begins.
                            ExecuteStart = DateTime.Now;

                            if (!string.IsNullOrEmpty(Reader))
                            {
                                GetSqlServerOutput(command, attempts);
                            }
                            else
                            {
                                command.ExecuteNonQuery();

                                try
                                {
                                    Logging.Logger.Aquire();
                                    Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "", TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                                    Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "   COMMAND COMPLETED: " + command.CommandText, TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                                    Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "           DURRATION: " + DateTime.Now.Subtract(ExecuteStart).ToString(@"dd\.hh\.\.mm\:ss\:fff"), TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                                    Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "             ATTEMPT: " + attempts + "/" + MaxAttempts, TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                                    Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "              THREAD: " + ThreadID, TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);

                                }
                                catch (Exception ex)
                                {
                                    throw ex;
                                }
                                finally
                                {
                                    Logging.Logger.Release();
                                }

                                GetNonCursorOutput(command, attempts);

                                //if (OnlyInputParams)
                                //{
                                //    if (!string.IsNullOrEmpty(Reader))
                                //    {
                                //        GetSqlServerOutput(command, attempts);
                                //    }
                                //    else
                                //    {
                                //        command.ExecuteNonQuery();

                                //        try
                                //        {
                                //            Logging.Logger.Aquire();
                                //            Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "", TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                                //            Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "   COMMAND COMPLETED: " + CommandText, TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                                //            Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "           DURRATION: " + DateTime.Now.Subtract(ExecuteStart).ToString(@"dd\.hh\.\.mm\:ss\:fff"), TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                                //            Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "             ATTEMPT: " + (attempts + 1), TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                                //            Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "              THREAD: " + ThreadID, TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);

                                //        }
                                //        catch (Exception ex)
                                //        {
                                //            throw ex;
                                //        }
                                //        finally
                                //        {
                                //            Logging.Logger.Release();
                                //        }
                                //    }
                                //}
                                //else if (OnlyStandardOutputExists)
                                //{
                                //    command.ExecuteNonQuery();

                                //    GetNonCursorOutput(command, attempts);
                                //}
                            }
                        }
                    }

                    break;
                }
                catch (Exception ex)
                {
                    LastError = ex;

                    //try
                    //{
                    //    Logging.Logger.Aquire();
                    //    Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "               ERROR: " + ex.Message, System.Diagnostics.TraceEventType.Error, 2, ThreadID, SharedData.LogCategory);
                    //    Logging.Logger.Write("DatabaseCommand.ExecuteMSSQLCommand", "          CONNECTION: " + (ConnectionString.Contains("Password") ? ConnectionString.Substring(0, ConnectionString.IndexOf("Password")) : ConnectionString), System.Diagnostics.TraceEventType.Error, 2, ThreadID, SharedData.LogCategory);
                    //}
                    //catch (Exception)
                    //{}
                    //finally 
                    //{ Logging.Logger.Release(); }

                    attempts += 1;
                    if (attempts > MaxAttempts)
                    {
                        LastError = new Exception(string.Format("Calling '{0}' -> {1}", CommandText, ex.Message), LastError);
                        //return;
                        throw LastError;
                    }
                }
            }
        }

        protected string BuildSqlServerParameters(SqlCommand command)
        {
            string details = "";

            if(ParameterCollection != null && ParameterCollection.Count > 0)
            {
                foreach(Parameter parameter in ParameterCollection.Items)
                {
                    AddSqlServerParameter(command, parameter);

                    if(parameter.Direction.ToLower() == "input")
                    {
                        if (parameter.DataType.ToLower() != "array")
                        {
                            details += "(" + parameter.Name + ": " + TextParser.Parse(parameter.Value, DrivingData, SharedData, StringCommands) + ") ";
                        }
                        else
                            details += " (" + parameter.Name + ": ARRAY) ";
                    }
                }
            }

            return details;
        }

        protected void AddSqlServerParameter(SqlCommand command, Parameter parameter)
        {
            string parsed_value = null;

            SqlParameter sql_parameter = null;

            switch(parameter.DataType.ToLower())
            {
                case "varchar":

                    if(parameter.Direction.ToLower() == "input")
                    {
                        parsed_value = TextParser.Parse(parameter.Value, DrivingData, SharedData, StringCommands);

                        if (!string.IsNullOrEmpty(parsed_value))
                        {
                            sql_parameter = new SqlParameter
                            {
                                ParameterName = parameter.Name,
                                SqlDbType     = SqlDbType.VarChar,
                                Value         = parsed_value,
                                Direction     = ParameterDirection.Input
                            };
                        }
                        else
                        {
                            sql_parameter = new SqlParameter
                            {
                                ParameterName = parameter.Name,
                                SqlDbType     = SqlDbType.VarChar,
                                Value         = DBNull.Value,
                                Direction     = ParameterDirection.Input
                            };
                        }
                    }
                    else if(parameter.Direction.ToLower() == "output")
                    {
                        sql_parameter = new SqlParameter
                        {
                            ParameterName = parameter.Name,
                            SqlDbType     = SqlDbType.VarChar,
                            Size          = 4000, // Max Size
                            Direction     = ParameterDirection.Output
                        };
                    }
                    
                    break;
                case "timestamp":

                    if(parameter.Direction.ToLower() == "input")
                    {
                        parsed_value = TextParser.Parse(parameter.Value, DrivingData, SharedData, StringCommands);

                        if (!string.IsNullOrEmpty(parsed_value))
                        {
                            sql_parameter = new SqlParameter
                            {
                                ParameterName = parameter.Name,
                                SqlDbType     = SqlDbType.Timestamp,
                                Value         = DateTime.Parse(parsed_value),
                                Direction     = ParameterDirection.Input
                            };
                        }
                        else
                        {
                            sql_parameter = new SqlParameter
                            {
                                ParameterName = parameter.Name,
                                SqlDbType     = SqlDbType.Timestamp,
                                Value         = DBNull.Value,
                                Direction     = ParameterDirection.Input
                            };
                        }
                    }
                    else if(parameter.Direction.ToLower() == "output")
                    {
                        sql_parameter = new SqlParameter
                        {
                            ParameterName = parameter.Name,
                            SqlDbType     = SqlDbType.Timestamp,
                            Direction     = ParameterDirection.Output
                        };
                    }

                    break;
                case "date":
                    if(parameter.Direction.ToLower() == "input")
                    {
                        parsed_value = TextParser.Parse(parameter.Value, DrivingData, SharedData, StringCommands);

                        if (!string.IsNullOrEmpty(parsed_value))
                        {
                            sql_parameter = new SqlParameter
                            {
                                ParameterName = parameter.Name,
                                SqlDbType     = SqlDbType.Date,
                                Value         = DateTime.Parse(parsed_value),
                                Direction     = ParameterDirection.Input
                            };
                        }
                        else
                        {
                            sql_parameter = new SqlParameter
                            {
                                ParameterName = parameter.Name,
                                SqlDbType     = SqlDbType.Date,
                                Value         = DBNull.Value,
                                Direction     = ParameterDirection.Input
                            };
                        }
                    }
                    else if(parameter.Direction.ToLower() == "output")
                    {
                        sql_parameter = new SqlParameter
                        {
                            ParameterName = parameter.Name,
                            SqlDbType     = SqlDbType.Date,
                            Direction     = ParameterDirection.Output
                        };
                    }

                    break;
                case "datetime":
                    if (parameter.Direction.ToLower() == "input")
                    {
                        parsed_value = TextParser.Parse(parameter.Value, DrivingData, SharedData, StringCommands);

                        if (!string.IsNullOrEmpty(parsed_value))
                        {
                            sql_parameter = new SqlParameter
                            {
                                ParameterName = parameter.Name,
                                SqlDbType = SqlDbType.DateTime,
                                Value = DateTime.Parse(parsed_value),
                                Direction = ParameterDirection.Input
                            };
                        }
                        else
                        {
                            sql_parameter = new SqlParameter
                            {
                                ParameterName = parameter.Name,
                                SqlDbType = SqlDbType.DateTime,
                                Value = DBNull.Value,
                                Direction = ParameterDirection.Input
                            };
                        }
                    }
                    else if (parameter.Direction.ToLower() == "output")
                    {
                        sql_parameter = new SqlParameter
                        {
                            ParameterName = parameter.Name,
                            SqlDbType = SqlDbType.Date,
                            Direction = ParameterDirection.Output
                        };
                    }

                    break;
                case "decimal":

                    if(parameter.Direction.ToLower() == "input")
                    {
                        parsed_value = TextParser.Parse(parameter.Value, DrivingData, SharedData, StringCommands);

                        if (!string.IsNullOrEmpty(parsed_value))
                        {
                            sql_parameter = new SqlParameter
                            {
                                ParameterName = parameter.Name,
                                SqlDbType     = SqlDbType.Decimal,
                                Value         = Convert.ToDecimal(parsed_value),
                                Direction     = ParameterDirection.Input
                            };
                        }
                        else
                        {
                            sql_parameter = new SqlParameter
                            {
                                ParameterName = parameter.Name,
                                SqlDbType     = SqlDbType.Decimal,
                                Value         = DBNull.Value,
                                Direction     = ParameterDirection.Input
                            };
                        }
                    }
                    else if(parameter.Direction.ToLower() == "output")
                    {
                        sql_parameter = new SqlParameter
                        {
                            ParameterName = parameter.Name,
                            SqlDbType     = SqlDbType.Decimal,
                            Direction     = ParameterDirection.Output
                        };
                    }

                    break;
                case "int":

                    if(parameter.Direction.ToLower() == "input")
                    {
                        parsed_value = TextParser.Parse(parameter.Value, DrivingData, SharedData, StringCommands);

                        if (!string.IsNullOrEmpty(parsed_value))
                        {
                            sql_parameter = new SqlParameter
                            {
                                ParameterName = parameter.Name,
                                SqlDbType     = SqlDbType.Int,
                                Value         = Convert.ToInt32(parsed_value),
                                Direction     = ParameterDirection.Input
                            };
                        }
                        else
                        {
                            sql_parameter = new SqlParameter
                            {
                                ParameterName = parameter.Name,
                                SqlDbType     = SqlDbType.Int,
                                Value         = DBNull.Value,
                                Direction     = ParameterDirection.Input
                            };
                        }
                    }
                    else if(parameter.Direction.ToLower() == "output")
                    {
                        sql_parameter = new SqlParameter
                        {
                            ParameterName = parameter.Name,
                            SqlDbType     = SqlDbType.Int,
                            Direction     = ParameterDirection.Output
                        };
                    }

                    break;
                default:
                    throw new Exception("The SQL parameter data type '" + parameter.DataType + "' is not supported.");
            }

            command.Parameters.Add(sql_parameter);
        }

        protected void GetNonCursorOutput(SqlCommand command, int attempt)
        {
            DataRow row     = null;
            bool has_output = false;

            if (!string.IsNullOrEmpty(Name))
            {
                foreach (SqlParameter parameter in command.Parameters)
                {
                    if (parameter.Direction == ParameterDirection.Output)
                    {
                        has_output = true;

                        switch (parameter.SqlDbType)
                        {
                            case SqlDbType.VarChar:
                                SharedData.Data.Tables(Name).Columns.Add(parameter.ParameterName, typeof(string));
                                break;
                            case SqlDbType.DateTime:
                                SharedData.Data.Tables(Name).Columns.Add(parameter.ParameterName, typeof(DateTime));
                                break;
                            case SqlDbType.Decimal:
                                SharedData.Data.Tables(Name).Columns.Add(parameter.ParameterName, typeof(decimal));
                                break;
                            case SqlDbType.Int:
                                SharedData.Data.Tables(Name).Columns.Add(parameter.ParameterName, typeof(int));
                                break;
                            default:
                                throw new Exception("The parameter data type '" + parameter.SqlDbType + "' is not currently supported.");
                        }
                    }
                }

                if (has_output)
                {
                    try
                    {
                        Logging.Logger.Aquire();
                        Logging.Logger.Write("DatabaseCommand.GetNonCursorOutput", "", TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                        Logging.Logger.Write("DatabaseCommand.GetNonCursorOutput", "   DESTINATION TABLE: " + Name, TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                        Logging.Logger.Write("DatabaseCommand.GetNonCursorOutput", "             ATTEMPT: " + (attempt + 1), TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                        Logging.Logger.Write("DatabaseCommand.GetNonCursorOutput", "              THREAD: " + ThreadID, TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                    finally
                    {
                        Logging.Logger.Release();
                    }

                    // Generate a new row from the table.
                    row = SharedData.Data.Tables(Name).NewRow();

                    // Add the output parameter values to the table.
                    foreach (SqlParameter parameter in command.Parameters)
                    {
                        if (parameter.Direction == ParameterDirection.Output)
                        {
                            Logging.Logger.Write("DatabaseCommand.GetNonCursorOutput", "              COLUMN: " + parameter.ParameterName + " VALUE: " + parameter.Value.ToString(), TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                            row[parameter.ParameterName] = parameter.Value;
                        }
                    }

                    // Add the row to the table.
                    SharedData.Data.Tables(Name).Rows.Add(row);
                }
            }
        }

        protected void GetSqlServerOutput(SqlCommand command, int attempt)
        {
            SqlDataAdapter adapter = null;

            if (!string.IsNullOrEmpty(Reader))
            {
                using (adapter = new SqlDataAdapter(command))
                {
                    adapter.Fill(SharedData.Data.Tables(Reader));

                    try
                    {
                        Logging.Logger.Aquire();
                        Logging.Logger.Write("Database.GetSqlServerOutput", "", TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                        Logging.Logger.Write("Database.GetSqlServerOutput", "   COMMAND COMPLETED: " + command.CommandText, TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                        Logging.Logger.Write("Database.GetSqlServerOutput", "            DURATION: " + DateTime.Now.Subtract(ExecuteStart).ToString(@"dd\.hh\.\.mm\:ss\:fff"), TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                        Logging.Logger.Write("Database.GetSqlServerOutput", "           ROW COUNT: " + SharedData.Data.Tables(Reader).Rows.Count, TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                        Logging.Logger.Write("Database.GetSqlServerOutput", "   DESTINATION TABLE: " + Reader, TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                        Logging.Logger.Write("Database.GetSqlServerOutput", "             ATTEMPT: " + (attempt + 1), TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
                        Logging.Logger.Write("Database.GetSqlServerOutput", "              THREAD: " + ThreadID, TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);
						//Logging.Logger.Write("Database.GetSqlServerOutput", "          CONNECTION: " + command.Connection.ConnectionString, TraceEventType.Information, 2, ThreadID, SharedData.LogCategory);

						Parent.GoToModule = GoToModuleOnNoData;
					}
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                    finally
                    {
                        Logging.Logger.Release();
                    }
                }
            }
        }

        public void OpenConnection(SqlConnection conn, string failure_email_subject, string failure_email)
        {
            int attempts        = 0;
            TimeSpan wait_time  = new TimeSpan(0, 0, 60);
            DateTime start_time = DateTime.Now;

            while(true)
            {
                try
                {
                    if (conn.State != ConnectionState.Open)
                    {
                        conn.Open();
                        //Logging.Logger.WriteLine("Database.OpenConnection", "Opening connection.", System.Diagnostics.TraceEventType.Information, 0, ThreadID, SharedData.LogCategory);
                    }   

                    // Check to see if we had connection failures.
                    if (attempts > 0)
                    {
                        try
                        {
                            Logging.Logger.Aquire();
                            Logging.Logger.Write("Database.OpenConnection", "", System.Diagnostics.TraceEventType.Information, 0, ThreadID, SharedData.LogCategory);
                            Logging.Logger.Write("Database.OpenConnection", "Connection error cleared after " + attempts.ToString("G") + " attempts", System.Diagnostics.TraceEventType.Information, 0, ThreadID, SharedData.LogCategory);
                        }
                        catch (Exception ex)
                        { throw ex; }
                        finally
                        {
                            Logging.Logger.Release();
                        }
                    }

                    break;
                }
                catch (SqlException ex)
                {
                    Console.WriteLine(ex);

                    if(attempts == 0)
                    {
                        try
                        {
                            Logging.Logger.Aquire();
                            Logging.Logger.Write("Database.OpenConnection", "", System.Diagnostics.TraceEventType.Information, 0, ThreadID, SharedData.LogCategory);
                            Logging.Logger.Write("Database.OpenConnection", "Database connection is currently unavailable.", System.Diagnostics.TraceEventType.Information, 0, ThreadID, SharedData.LogCategory);
                            Logging.Logger.Write("Database.OpenConnection", ex.Message, System.Diagnostics.TraceEventType.Information, 0, ThreadID, SharedData.LogCategory);
                        }
                        catch (Exception ex1)
                        { throw ex1; }
                        finally
                        {
                            Logging.Logger.Release();
                        }

                        // TODO: Send email notification here.
                    }

                    attempts += 1;
                    Thread.Sleep(wait_time);
                }
            }
        }

        protected void AddDatabaseCommandTables()
        {
            List<string> all_table_names = AllDataSetTableNames;

            if(all_table_names != null && all_table_names.Count > 0)
            {
                foreach(string table_name in all_table_names)
                {
                    try
                    {
                        Logging.Logger.Aquire();
                        Logging.Logger.Write("DatabaseCommand.AddDatabaseCommandTables", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        Logging.Logger.Write("DatabaseCommand.AddDatabaseCommandTables", " ADDING GLOBAL TABLE: " + table_name, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        Logging.Logger.Write("DatabaseCommand.AddDatabaseCommandTables", "              THREAD: " + ThreadID, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    }
                    catch (Exception ex)
                    { throw ex; }
                    finally
                    {
                        Logging.Logger.Release();
                    }

					if (!SharedData.Data.Contains(table_name))
					{
						SharedData.Data.Add(table_name);
					}
                }

                all_table_names = null;
            }
        }

        public List<StringCommand> GetStringCommands()
        {
            return new List<StringCommand> 
            { 
                new StringCommand
                {
                    Name = "%ThreadID%",
                    Value = ThreadID.ToString()
                },
                new StringCommand
                {
                    Name = "%DateTimeNow%",
                    Value = DateTime.Now.ToString()
                }
            };
        }
    }

    public class DatabaseConnection
    {
        [XmlAttribute(AttributeName = "Type")]
        public string Type { get; set; }

        [XmlAttribute(AttributeName = "String")]
        public string String { get; set; }

        [XmlAttribute(AttributeName = "Timeout")]
        public int Timeout = 0;
    }

    public class ParameterCollection
    {
        [XmlElement(ElementName = "Parameter")]
        public List<Parameter> Items { get; set; }

        public int Count
        {
            get
            {
                if (Items != null)
                    return Items.Count;

                return 0;
            }
        }
    }

    public class Parameter
    {
        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "DataSetTableName")]
        public string DataSetTableName { get; set; }

        [XmlAttribute(AttributeName = "DataType")]
        public string DataType { get; set; }

        [XmlAttribute(AttributeName = "Value")]
        public string Value { get; set; }

        [XmlAttribute(AttributeName = "Direction")]
        public string Direction = "Input";

		[XmlElement(ElementName = "Path")]
		public string Path { get; set; }
	}
}
