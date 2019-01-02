using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace WFM.Data
{
    public class CacheTableCollection
    {
        [XmlElement(ElementName = "SourceDataTable")]
        public List<CacheTable> Items { get; set; }

        public int Count
        {
            get
            {
                if (Items != null)
                    return Items.Count;

                return 0;
            }
        }

        public int Length
        {
            get
            {
                if (Items != null)
                    return Items.Count - 1;

                return -1;
            }
        }
    }

    public class CacheTable
    {
        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "DrivingTableName")]
        public string DrivingTableName { get; set; }

        [XmlElement(ElementName = "SourceDataTables")]
        public CacheTableCollection CacheTableCollection { get; set; }

        [XmlElement(ElementName = "DataColumns")]
        public CacheColumnCollection CacheColumnCollection { get; set; }

        [XmlElement(ElementName = "Union")]
        public UnionOperation Union { get; set; }

        [XmlElement(ElementName = "Join")]
        public JoinOperation Join { get; set; }

        [XmlElement(ElementName = "Filter")]
        public GenericExpression Filter { get; set; }

        [XmlElement(ElementName = "Sort")]
        public GenericExpression Sort { get; set; }

        [XmlElement(ElementName = "Aggregate")]
        public GenericExpression Aggregate { get; set; }

        private Cache SharedData { get; set; }

        private DataRow DrivingData { get; set; }

        public string AggregateExpression
        {
            get
            {
                if(Aggregate != null && !string.IsNullOrEmpty(Aggregate.Expression) && Aggregate.Expression.Length > 0)
                {
                    return TextParser.Parse(Aggregate.Expression, DrivingData, SharedData, null);
                }

                return null;
            }
        }

        public string FilterExpression
        {
            get
            {
                if (Filter != null && !string.IsNullOrEmpty(Filter.Expression) && Filter.Expression.Length > 0)
                {
                    return TextParser.Parse(Filter.Expression, DrivingData, SharedData, null);
                }

                return null;
            }
        }

        public string SortExpression
        {
            get
            {
                if (Sort != null && !string.IsNullOrEmpty(Sort.Expression) && Sort.Expression.Length > 0)
                {
                    return TextParser.Parse(Sort.Expression, DrivingData, SharedData, null);
                }

                return null;
            }
        }

        public CacheTable()
        {

        }

        public CacheTable(Cache cache, DataRow driving_data, CacheTable configuration)
        {
            SharedData            = cache;
            DrivingData           = driving_data;
            Name                  = configuration.Name;
            DrivingTableName      = configuration.DrivingTableName;
            CacheTableCollection  = configuration.CacheTableCollection;
            CacheColumnCollection = configuration.CacheColumnCollection;
            Union                 = configuration.Union;
            Join                  = configuration.Join;
            Filter                = configuration.Filter;
            Sort                  = configuration.Sort;
            Aggregate             = configuration.Aggregate;
        }

        private DataTable PerformUnion(CacheTable cache_table)
        {
            DataTable data_table1, data_table2;

            if (cache_table.Union != null)
            {
                data_table1 = PerformUnion(cache_table.Union.DataTable);

                data_table2 = SharedData.Data.Tables(cache_table.Name).Copy();

                data_table1.Merge(data_table2);

                data_table1.TableName = cache_table.Union.NewTableName;

                return data_table1;
            }
            else
                return SharedData.Data.Tables(cache_table.Name).Copy();
        }

        private DataTable PerformJoin(DataTable source_table)
        {
            DataTable table_to_join = new DataTable();
            DataTable new_source_table = new DataTable();
            DataRow nRow = null;

            if(Join != null)
            {
                // Get the table that will be joined with the source table from the global cacheset.
                table_to_join = SharedData.Data.Tables(Join.DataTable.Name);

                // Add each column in the source table to the new table.
                foreach(DataColumn source_column in source_table.Columns)
                {
                    new_source_table.Columns.Add(new DataColumn
                    {
                        ColumnName   = source_column.ColumnName,
                        DataType     = source_column.DataType,
                        DefaultValue = source_column.DefaultValue 
                    });
                }

                // Add each column in the table to join that is not the Fkey to the new source table.
                foreach(DataColumn join_column in table_to_join.Columns)
                {
                    // Make sure the column is not the key column and that the column was not already
                    // added from the source table.
                    if(join_column.ColumnName != Join.ForeignKey && !new_source_table.Columns.Contains(join_column.ColumnName))
                    {
                        // See if only certain columns are specified in the configuration.
                        if(Join.DataTable.CacheColumnCollection != null)
                        {
                            // Make sure the column is listed in the configuration.
                            if(Join.DataTable.CacheColumnCollection.Contains(join_column.ColumnName))
                            {
                                new_source_table.Columns.Add(new DataColumn
                                {
                                    ColumnName   = join_column.ColumnName,
                                    DataType     = join_column.DataType,
                                    DefaultValue = join_column.DefaultValue
                                });
                            }
                        }
                        else
                        {
                            new_source_table.Columns.Add(new DataColumn
                            {
                                ColumnName   = join_column.ColumnName,
                                DataType     = join_column.DataType,
                                DefaultValue = System.DBNull.Value
                            });
                        }
                    }
                }

                // Grab each row in table to join.
                foreach(DataRow jRow in table_to_join.Rows)
                {
                    // Grab each row in the source table.
                    foreach(DataRow sRow in source_table.Rows)
                    {
                        // See if the PKey column in the source table matches the Fkey column in the table 
                        // to join column.
                        if(sRow[Join.PrimaryKey].ToString() == jRow[Join.ForeignKey].ToString())
                        {
                            // Create a new row for the new data source table.
                            nRow = new_source_table.NewRow();

                            foreach(DataColumn sCol in source_table.Columns)
                            {
                                // Copy the value of each column in the old source table's row to the
                                // new source table's row.
                                if(!string.IsNullOrEmpty(sRow[sCol.ColumnName].ToString()))
                                {
                                    nRow[sCol.ColumnName] = sRow[sCol.ColumnName].ToString();
                                }
                            }

                            foreach(DataColumn jCol in table_to_join.Columns)
                            {
                                // Make sure the current column is not the key column.
                                if(jCol.ColumnName == Join.ForeignKey)
                                {
                                    // Copy the value of each column in the table to join's row to the
                                    // new source table's row.
                                    if(!string.IsNullOrEmpty(jRow[jCol.ColumnName].ToString()))
                                    {
                                        nRow[jCol.ColumnName] = jRow[jCol.ColumnName].ToString();
                                    }
                                }
                            }

                            // Add the new row to the new source table.
                            new_source_table.Rows.Add(nRow);

                            break;
                        }
                    }
                }

                // Name the source table.
                new_source_table.TableName = Join.NewTableName;

                // Add the new source table to the global dataset.
                SharedData.Add(new_source_table);

                return new_source_table;
            }

            return source_table;
        }

        public DataTable Process()
        {
            return Process(CacheTableCollection);
        }

        public DataTable Process(CacheTableCollection conditional_tables)
        {
            DataTable table, filtered_table;

            // Perform the union and join operations on the table.
            table = PerformJoin(PerformUnion(this));

            // Copy the table structure/columns to the new table.
            filtered_table = table.Clone();

            // Perform the filter and sort operations against the table.
            foreach(DataRow row in table.Select(FilterExpression, SortExpression))
            {
                filtered_table.Rows.Add(row.ItemArray);
            }

            // Check for results.
            if(filtered_table.Rows.Count > 0)
            {
                // Check for condition tables. Condition tables are defined in the destination tables. They can be used
                // to determine if the data being processed should be used by running filters on any data table that
                // exists in the cache. If a filter produces no results, the data will be discarded.
                if(conditional_tables != null)
                {
                    foreach(CacheTable condition_table in conditional_tables.Items)
                    {
                        // Check to see if the current condition table exists.
                        if(SharedData.Data.Contains(condition_table.Name))
                        {
                            // Run the condition table's filter to see if it has any results.
                            if(SharedData.Data.Tables(condition_table.Name).Select(condition_table.FilterExpression).Length <= 0)
                            {
                                // The current condition was not met. Discard the data and return an empty table.
                                filtered_table.Rows.Clear();
                            }
                        }
                    }
                }
            }

            filtered_table.AcceptChanges();
            return filtered_table;
        }
    }

    public class CacheColumnCollection
    {
        private List<CacheColumn> items;

        [XmlElement(ElementName = "Column")]
        public List<CacheColumn> Items
        {
            get
            {
                if (items == null)
                    items = new List<CacheColumn>();

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

        public int Length
        {
            get
            {
                if (Items != null)
                    return Items.Count - 1;

                return -1;
            }
        }

        public void Add(string name, string data_type, string value)
        {
            Items.Add(new CacheColumn
            {
                Name     = name,
                DataType = data_type,
                Value    = value
            });
        }

        public bool Contains(string column_name)
        {
            foreach(CacheColumn column in Items)
            {
                if (column.Name == column_name)
                    return true;
            }

            return false;
        }

        public CacheColumn GetColumn(int index)
        {
            return Items[index];
        }

        public void SetColumn(int index, CacheColumn column)
        {
            Items[index] = column;
        }
    }

    public class GenericExpression
    {
        [XmlAttribute(AttributeName = "Expression")]
        public string Expression = "";
    }

    public class CacheColumn
    {
        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "Description")]
        public string Description { get; set; }

        [XmlAttribute(AttributeName = "DataType")]
        public string DataType { get; set; }

        [XmlAttribute(AttributeName = "Value")]
        public string Value { get; set; }

        [XmlAttribute(AttributeName = "Width")]
        public int Width { get; set; }

        [XmlAttribute(AttributeName = "Format")]
        public string Format { get; set; }

        public Type GetDataType()
        {
            string type = DataType.Substring(0,1).ToUpper() + DataType.Substring(1).ToLower();

            if(type == "Date" || type == "Time")
            {
                type = "DateTime";
            }
            else if(type == "Byte[]")
            {
                return typeof(Byte[]);
            }

            return System.Type.GetType("System." + type);
        }
    }

    public class JoinOperation
    {
        [XmlAttribute(AttributeName = "PrimaryKey")]
        public string PrimaryKey { get; set; }

        [XmlAttribute(AttributeName = "ForeignKey")]
        public string ForeignKey { get; set; }

        [XmlAttribute(AttributeName = "NewTableName")]
        public string NewTableName { get; set; }

        [XmlElement(ElementName = "DataTable")]
        public CacheTable DataTable { get; set; }
    }

    public class UnionOperation
    {
        [XmlAttribute(AttributeName = "TableName")]
        public string TableName { get; set; }

        [XmlAttribute(AttributeName = "NewTableName")]
        public string NewTableName { get; set; }

        [XmlElement(ElementName = "DataTable")]
        public CacheTable DataTable { get; set; }
    }
}
