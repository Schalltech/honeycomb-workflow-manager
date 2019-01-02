using Schalltech.EnterpriseLibrary.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace WFM.Data
{
    public class Cache
    {
        private CacheSet cache_set;

        private LogCategory log_category = null;

        public CacheSet Data
        {
            get
            {
                return cache_set;
            }
        }

        public DateTime StartDate
        {
            get
            {
                return Convert.ToDateTime(Data.Tables("DefaultSettings").Rows[0]["StartDate"].ToString());
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["StartDate"] = value;
            }
        }

        public DateTime EndDate
        {
            get
            {
                return Convert.ToDateTime(Data.Tables("DefaultSettings").Rows[0]["EndDate"].ToString());
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["EndDate"] = value;
            }
        }

        public DateTime CurrentDate
        {
            get
            {
                return Convert.ToDateTime(Data.Tables("DefaultSettings").Rows[0]["CurrentDate"].ToString());
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["CurrentDate"] = value;
            }
        }

        public DateTime ProcessDate
        {
            get
            {
                return Convert.ToDateTime(Data.Tables("DefaultSettings").Rows[0]["ProcessDate"].ToString());
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["ProcessDate"] = value;
            }
        }

        public string ProcessName
        {
            get
            {
                return Data.Tables("DefaultSettings").Rows[0]["ProcessName"].ToString();
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["ProcessName"] = value;
            }
        }

        public string ProcessType
        {
            get
            {
                return Data.Tables("DefaultSettings").Rows[0]["ProcessType"].ToString();
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["ProcessType"] = value;
            }
        }

        public LogCategory LogCategory
        {
            get
            {
                return log_category;
            }
            set
            {
                log_category = value;

                if(log_category != null)
                {
                    if(!string.IsNullOrEmpty(log_category.Name))
                    {
                        log_category.Name = TextParser.Parse(log_category.Name, null, this, null);
                    }

                    if(log_category.Appenders != null)
                    {
                        foreach(RollingFileAppenderConfiguration appender in value.Appenders)
                        {
                            if(!string.IsNullOrEmpty(appender.FileName))
                            {
                                appender.FileName = System.Reflection.Assembly.GetEntryAssembly().GetName().Name + " - " + TextParser.Parse(appender.FileName, null, this, null);
                            }
                        }
                    }
                }
            }
        }

        public string TempFileDirectory
        {
            get
            {
                return Data.Tables("DefaultSettings").Rows[0]["TempFileDirectory"].ToString();
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["TempFileDirectory"] = value;
            }
        }

        public DateTime RunDate
        {
            get
            {
                return Convert.ToDateTime(Data.Tables("DefaultSettings").Rows[0]["RunDate"].ToString());
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["RunDate"] = value;
            }
        }

        public string MachineName
        {
            get
            {
                return Data.Tables("DefaultSettings").Rows[0]["MachineName"].ToString();
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["MachineName"] = value;
            }
        }

        public string ProcessState
        {
            get
            {
                return Data.Tables("DefaultSettings").Rows[0]["ProcessState"].ToString();
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["ProcessState"] = value;
            }
        }

        public string ProcessStatus
        {
            get
            {
                return Data.Tables("DefaultSettings").Rows[0]["ProcessStatus"].ToString();
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["ProcessStatus"] = value;
            }
        }

        public List<string> Args
        {
            get
            {
                List<string> collection = new List<string>();

                foreach(DataRow row in Data.Tables("Args").Rows)
                    collection.Add(row["Arg"].ToString());

                return collection;
            }
            set
            {
                DataRow new_row = null;

                Data.Tables("Args").Clear();

                foreach(string arg in value)
                {
                    new_row = Data.Tables("Args").NewRow();
                    new_row["Arg"] = arg;
                    Data.Tables("Args").Rows.Add(new_row);
                }
            }
        }

        public string Arg1
        {
            get
            {
                return Data.Tables("DefaultSettings").Rows[0]["Arg1"].ToString();
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["Arg1"] = value;
            }
        }

        public string Arg2
        {
            get
            {
                return Data.Tables("DefaultSettings").Rows[0]["Arg2"].ToString();
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["Arg2"] = value;
            }
        }

        public string Arg3
        {
            get
            {
                return Data.Tables("DefaultSettings").Rows[0]["Arg3"].ToString();
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["Arg3"] = value;
            }
        }

        public string Arg4
        {
            get
            {
                return Data.Tables("DefaultSettings").Rows[0]["Arg4"].ToString();
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["Arg4"] = value;
            }
        }

        public string Arg5
        {
            get
            {
                return Data.Tables("DefaultSettings").Rows[0]["Arg5"].ToString();
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["Arg5"] = value;
            }
        }

        public string Arg6
        {
            get
            {
                return Data.Tables("DefaultSettings").Rows[0]["Arg6"].ToString();
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["Arg6"] = value;
            }
        }

        public string Arg7
        {
            get
            {
                return Data.Tables("DefaultSettings").Rows[0]["Arg7"].ToString();
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["Arg7"] = value;
            }
        }

        public string Arg8
        {
            get
            {
                return Data.Tables("DefaultSettings").Rows[0]["Arg8"].ToString();
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["Arg8"] = value;
            }
        }

        public string Arg9
        {
            get
            {
                return Data.Tables("DefaultSettings").Rows[0]["Arg9"].ToString();
            }
            set
            {
                Data.Tables("DefaultSettings").Rows[0]["Arg9"] = value;
            }
        }

        public Cache()
        {
            DataTable default_settings = null;
            DataColumn column = null;

            cache_set = new CacheSet();

            default_settings = new DataTable("DefaultSettings");
            
            column          = new DataColumn("StartDate");
            column.DataType = System.Type.GetType("System.DateTime");
            default_settings.Columns.Add(column);

            column          = new DataColumn("EndDate");
            column.DataType = System.Type.GetType("System.DateTime");
            default_settings.Columns.Add(column);

            column          = new DataColumn("CurrentDate");
            column.DataType = System.Type.GetType("System.DateTime");
            default_settings.Columns.Add(column);

            column          = new DataColumn("ProcessDate");
            column.DataType = System.Type.GetType("System.DateTime");
            default_settings.Columns.Add(column);

            column          = new DataColumn("ProcessName");
            column.DataType = System.Type.GetType("System.String");
            default_settings.Columns.Add(column);

            column          = new DataColumn("Application");
            column.DataType = System.Type.GetType("System.String");
            default_settings.Columns.Add(column);

            column          = new DataColumn("ProcessType");
            column.DataType = System.Type.GetType("System.String");
            default_settings.Columns.Add(column);

            column          = new DataColumn("TempFileDirectory");
            column.DataType = System.Type.GetType("System.String");
            default_settings.Columns.Add(column);

            column          = new DataColumn("MachineName");
            column.DataType = System.Type.GetType("System.String");
            default_settings.Columns.Add(column);

            column = new DataColumn("ProcessState");
            column.DataType = System.Type.GetType("System.String");
            column.DefaultValue = "EXECUTING";
            default_settings.Columns.Add(column);

            column = new DataColumn("ProcessStatus");
            column.DataType = System.Type.GetType("System.String");
            column.DefaultValue = "Process is running.";
            default_settings.Columns.Add(column);

            column          = new DataColumn("Arg1");
            column.DataType = System.Type.GetType("System.String");
            default_settings.Columns.Add(column);

            column          = new DataColumn("Arg2");
            column.DataType = System.Type.GetType("System.String");
            default_settings.Columns.Add(column);

            column          = new DataColumn("Arg3");
            column.DataType = System.Type.GetType("System.String");
            default_settings.Columns.Add(column);

            column          = new DataColumn("Arg4");
            column.DataType = System.Type.GetType("System.String");
            default_settings.Columns.Add(column);

            column          = new DataColumn("Arg5");
            column.DataType = System.Type.GetType("System.String");
            default_settings.Columns.Add(column);

            column          = new DataColumn("Arg6");
            column.DataType = System.Type.GetType("System.String");
            default_settings.Columns.Add(column);

            column          = new DataColumn("Arg7");
            column.DataType = System.Type.GetType("System.String");
            default_settings.Columns.Add(column);

            column          = new DataColumn("Arg8");
            column.DataType = System.Type.GetType("System.String");
            default_settings.Columns.Add(column);

            column          = new DataColumn("Arg9");
            column.DataType = System.Type.GetType("System.String");
            default_settings.Columns.Add(column);

            default_settings.Rows.Add(default_settings.NewRow());

            DataTable args = new DataTable("Args");

            column          = new DataColumn("Arg");
            column.DataType = System.Type.GetType("System.String");
            args.Columns.Add(column);

            Data.Add(default_settings);
            Data.Add(args);
        }

        public Cache(Cache cache)
        {
            cache_set = cache.cache_set;
        }

        public void Add(DataTable table)
        {
            Data.Add(table);
        }

        public void Add(string table_name)
        {
            Data.Add(new DataTable(table_name));
        }

        public void Add(string name, CacheTable table)
        {
            DataTable dt = new DataTable(name);
            DataColumn dc = null;

            foreach (CacheColumn column in table.CacheColumnCollection.Items)
            {
                dc = new DataColumn(column.Name, column.GetDataType());
                dt.Columns.Add(dc);
            }

            cache_set.Add(dt);
        }
    }

    public class CacheSet
    {
        private List<DataTable> data_tables { get; set; }
        private Mutex mutex { get; set; }

        public CacheSet()
        {
            data_tables = new List<DataTable>();
            mutex       = new Mutex();
        }

        public List<DataTable> DataTables
        {
            get
            {
                return data_tables;
            }
        }

        /// <summary>
        /// Adds a new System.Data.DataTable with the specified name to the CacheSet.
        /// </summary>
        /// <param name="name">The name of the table.</param>
        public void Add(string name)
        {
            Add(new DataTable(name));
        }

        /// <summary>
        /// Adds the specified Syste.Data.DataTable to the CacheSet.
        /// </summary>
        /// <param name="table">The table.</param>
        public void Add(DataTable table)
        {
            try
            {
                mutex.WaitOne();

                if (!Contains(table.TableName))
                {
                    DataTables.Add(table);
                }
                else
                {
                    throw new Exception("A data table with the name '" + table.TableName + "' already exists in the cache set.");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        public DataTable Tables(int index)
        {
            try
            {
                mutex.WaitOne();

                if (data_tables.Count >= index)
                    return data_tables[index];
                
                return null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        public DataTable Tables(string name)
        {
            try
            {
                mutex.WaitOne();

                foreach (DataTable table in data_tables)
                {
                    if (table.TableName == name)
                        return table;
                }

                return null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        public bool Contains(string name)
        {
            try
            {
                mutex.WaitOne();

                foreach (DataTable table in data_tables)
                {
                    if(table.TableName == name)
                        return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
    }
}
