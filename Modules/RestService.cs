using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using WFM.Data;
using WFM.Logging;

namespace WFM.Modules
{
	public class RestService : BaseModule
	{
		[XmlAttribute(AttributeName = "Verb")]
		public string Verb { get; set; }

		[XmlElement(ElementName = "DrivingModule")]
		public CacheTable DrivingModule { get; set; }

		[XmlElement(ElementName = "Endpoint")]
		public string Endpoint { get; set; }

		[XmlElement(ElementName = "Parameters")]
		public ParameterCollection ParameterCollection { get; set; }

		public RestService()
		{ }

		public RestService(Cache shared_data, RestService configuration)
			: base(shared_data, configuration)
		{
			Name				= configuration.Name;
			Endpoint			= TextParser.Parse(configuration.Endpoint, DrivingData, SharedData, ModuleCommands);
			Verb				= configuration.Verb;
			ParameterCollection = configuration.ParameterCollection;

			if(configuration.DrivingModule != null)
				DrivingModule		= new CacheTable(SharedData, DrivingData, configuration.DrivingModule);
		}

		protected override void OnProcess(object sender, EventArgs e)
		{
			System.Data.DataTable DrivingTable;
			int index = 0;

			try
			{
				Logger.Aquire();
				Logger.Write("RestService.OnProcess", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
				Logger.Write("RestService.OnProcess", "            ENDPOINT: " + TextParser.Parse(Endpoint, DrivingData, SharedData, ModuleCommands), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
				Logger.Write("RestService.OnProcess", "                VERB: " + TextParser.Parse(Verb, DrivingData, SharedData, ModuleCommands), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

				if (DrivingModule != null)
				{
					Logger.Write("RestService.OnProcess", "       SOURCE MODULE: " + DrivingModule.Name, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

					if (DrivingModule.Filter != null && !String.IsNullOrEmpty(DrivingModule.Filter.Expression))
					{
						Logger.Write("RestService.OnProcess", "              FILTER: " + TextParser.Parse(DrivingModule.Filter.Expression, DrivingData, SharedData, ModuleCommands), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
					}
					else
					{
						Logger.Write("RestService.OnProcess", "              FILTER: None", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
					}

					//Get the driving module from the shared data.
					if (SharedData.Data.Contains(DrivingModule.Name))
					{
						DrivingTable = DrivingModule.Process();

						Logger.Write("RestService.OnProcess", "           ROW COUNT: " + DrivingTable.Rows.Count, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
						Logger.Write("RestService.OnProcess", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

						foreach (System.Data.DataRow row in DrivingTable.Rows)
						{
							DrivingData = row;
							index++;

							Logger.Write("RestService.OnProcess", "    INVOKING SERVICE: " + index, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

							Invoke();
						}
					}
					else
						throw new Exception(string.Format("The referenced source table {0} does not exist in the global cache set.", DrivingModule.Name));
				}
				else
				{
					Invoke();
				}
			}
			catch (Exception ex)
			{
				throw ex;
			}
			finally
			{
				Logger.Release();
			}
		}

		private void Invoke()
		{
			using (HttpClient client = new HttpClient())
			{
				string url   = Endpoint;
				string query = Endpoint.EndsWith("/") ? "" : "/";

				//Define Headers
				client.DefaultRequestHeaders.Accept.Clear();
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("NOBRAINER2\\erics2:lilMerc02")));

				Dictionary<string, string> body = new Dictionary<string, string>();

				//Logger.Write("RestService.Invoke", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
				Logger.Write("RestService.Invoke", "          PARAMETERS: ", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

				if (ParameterCollection != null && ParameterCollection.Count > 0)
				{
					foreach (Parameter param in ParameterCollection.Items)
					{
						Logger.Write("RestService.Invoke", param.Name.PadLeft(20, ' ') + ": '" + TextParser.Parse(param.Value, DrivingData, SharedData, ModuleCommands) + "'", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
						//Logger.Write("RestService.Invoke", "                      " + param.Name + " = '" + TextParser.Parse(param.Value, DrivingData, SharedData, ModuleCommands) + "'", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

						if (param.DataType == "body")
						{
							if (String.IsNullOrEmpty(param.Path))
							{
								body.Add(param.Name, TextParser.Parse(param.Value, DrivingData, SharedData, ModuleCommands));
							}
							else
							{
								JObject json = JObject.Parse(TextParser.Parse(param.Value, DrivingData, SharedData, ModuleCommands));

								var x = json[param.Path];

								body.Add(param.Name, x.ToString());
							}
						}
						else if (param.DataType == "query")
						{
							if (String.IsNullOrEmpty(param.Path))
							{
								query += query.EndsWith("/") ? "" : "/" + TextParser.Parse(param.Value, DrivingData, SharedData, ModuleCommands);

								// query += param.Name + "=" + TextParser.Parse(param.Value, DrivingData, SharedData, ModuleCommands) + "&";
							}
							else
							{
								JObject json = JObject.Parse(TextParser.Parse(param.Value, DrivingData, SharedData, ModuleCommands));

								var x = json[param.Path];

								query += param.Name + "=" + x.ToString() + "&";

								query += query.EndsWith("/") ? "" : "/" + x.ToString();
							}
						}
						else if (param.DataType == "query2")
						{
							if (String.IsNullOrEmpty(param.Path))
							{
								query += param.Name + "=" + TextParser.Parse(param.Value, DrivingData, SharedData, ModuleCommands) + "&";
							}
							else
							{
								JObject json = JObject.Parse(TextParser.Parse(param.Value, DrivingData, SharedData, ModuleCommands));

								var x = json[param.Path];

								query += param.Name + "=" + x.ToString() + "&";
							}
						}
					}

					if (!string.IsNullOrEmpty(query))
						url += query;
				}

				FormUrlEncodedContent requestBody = new FormUrlEncodedContent(body);

				HttpResponseMessage request = null;

				switch (Verb.ToLower())
				{
					case "get":
						request = client.GetAsync(url).Result;
						break;
					case "post":
						request = client.PostAsync(url, requestBody).Result;
						break;
					case "put":
						request = client.PutAsync(url, requestBody).Result;
						break;
					case "delete":
						request = client.DeleteAsync(url).Result;
						break;
				}

				var response = request.Content.ReadAsStringAsync();

				SetModuleCommand("%ID%",       Convert.ToString(response.Id));
				SetModuleCommand("%Status%",   response.Status.ToString());
				SetModuleCommand("%Response%", response.Result);

				//Add the current file to the results table.
				AddResults();

				Logger.Write("RestService.Invoke", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
				Logger.Write("RestService.Invoke", "              STATUS: " + response.Status, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
				Logger.Write("RestService.Invoke", "              RESULT: " + response.Result, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
				Logger.Write("RestService.Invoke", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
			}
		}

		protected override void OnLoadVariables(object sender, EventArgs e)
		{
			AddDefaultModuleVariable("ID",		 "String", "%ID%");
			AddDefaultModuleVariable("Status",	 "String", "%Status%");
			AddDefaultModuleVariable("Response", "String", "%Response%");

			base.OnLoadVariables(sender, e);
		}
	}
}
