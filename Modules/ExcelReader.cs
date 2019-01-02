using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using WFM.Data;
using WFM.Logging;
using SpreadsheetGear;

namespace WFM.Modules
{
	public class ExcelReader : BaseFileReader
	{
		[XmlAttribute(AttributeName = "PageIndex")]
		public string PageIndex { get; set; }

		[XmlAttribute(AttributeName = "FontStripStrikethrough")]
		public bool FontStripStrikethrough { get; set; }

		public ExcelReader()
		{ }

		public ExcelReader(Cache shared_data, ExcelReader configuration)
			: base(shared_data, configuration)
		{
			if (!string.IsNullOrEmpty(configuration.PageIndex))
			{
				if (configuration.PageIndex.StartsWith("%"))
				{
					PageIndex = TextParser.Parse(configuration.PageIndex, DrivingData, SharedData, ModuleCommands);
				}
				else
				{
					PageIndex = configuration.PageIndex;
				}
			}
			else
			{
				PageIndex = "0";
			}

			FontStripStrikethrough = configuration.FontStripStrikethrough;

			Close += OnClose;
		}

		protected override void OnLoad(object sender, EventArgs e)
		{
			if (FontStripStrikethrough)
			{
				// TODO
			}

			CompleteFileContents = ((IWorkbook)LoadedFile).GetDataSet(SpreadsheetGear.Data.GetDataFlags.FormattedText | SpreadsheetGear.Data.GetDataFlags.NoColumnTypes).Tables[Convert.ToInt32(PageIndex)];
		}

		protected override void OnOpen(object sender, EventArgs e)
		{
			LoadedFile = SpreadsheetGear.Factory.GetWorkbook(FilePath + FileName);
		}

		protected void OnClose(object sender, EventArgs e)
		{
			if (LoadedFile != null)
			{
				((IWorkbook)LoadedFile).Close();
				LoadedFile = null;
			}
		}
	}
}
