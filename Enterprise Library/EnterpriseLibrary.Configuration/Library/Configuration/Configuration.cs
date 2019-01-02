using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Schalltech.EnterpriseLibrary.Configuration
{
    public class ConfigurationManager
    {
        static private Dictionary<string, object> configuration_cache = new Dictionary<string, object>();

        static public T GetConfiguration<T>(string key)
        {
            return GetConfiguration<T>(key, false);
        }

        static public T GetConfiguration<T>(string key, bool refresh_cache)
        {
            try
            {
                // Get the content of the configuration file.
                if (System.Configuration.ConfigurationManager.AppSettings[key] != null)
                {
                    return LoadConfiguration<T>(System.Configuration.ConfigurationManager.AppSettings[key], refresh_cache);
                }
                else
                {
                    throw new Exception("The key [" + key + "] does not exist in the applications appsettings collection.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Unable to deserialize the configuration." + Environment.NewLine + "Type: " + typeof(T).Name + Environment.NewLine + "Key: " + key, ex);
            }
        }

        static public T LoadConfiguration<T>(string path)
        {
            return LoadConfiguration<T>(path, false);
        }

        static public T LoadConfiguration<T>(string path, bool refresh_cache)
        {
            string content = null;
            System.Xml.Serialization.XmlSerializer serializer;
            object configuration;

            try
            {
                System.Threading.Monitor.Enter(configuration_cache);

                // Check to see if we need to refresh the cache.
                if (refresh_cache || !configuration_cache.ContainsKey(path))
                {
                    // Access the xml for the configuration.
                    // This accounts for the xtra xml nodes specific to the Microsoft Enterprise Library and attempts to skip
                    // those nodes by starting at the element that matches the type name of the object the configuration will be deserialized into.
                    using (XmlTextReader reader = new XmlTextReader(IO.Path.GetAbsolutePath(path)))
                    {
                        reader.ReadToFollowing(typeof(T).Name);
                        content = reader.ReadOuterXml();
                    }

                    // Verify the expected node in the configuration file was found.
                    if (!String.IsNullOrEmpty(content))
                    {
                        serializer = new System.Xml.Serialization.XmlSerializer(typeof(T));

                        // Deserialize the configuration into the expected object type.
                        configuration = serializer.Deserialize(new System.IO.StringReader(content));

                        if (configuration_cache.ContainsKey(path))
                        {
                            // Refresh the existing cached value.
                            configuration_cache[path] = configuration;
                        }
                        else
                        {
                            // Add the configuration to the cache.
                            configuration_cache.Add(path, configuration);
                        }
                    }
                    else
                    {
                        // The expected node was not found. A node matching the name of the expected object type must be present
                        // in the configurations xml in order to perform the deserialization.
                        throw new Exception("The configuration file does not contain the expected node <" + typeof(T).Name + " />. Please review the configuration file located at " + IO.Path.GetAbsolutePath(path) + ".");
                    }
                }
                else
                {
                    // Pull the configuration object from the cache.
                    configuration = configuration_cache[path];
                }

                // Return the configuration object.
                return (T)Convert.ChangeType(configuration, typeof(T));
            }
            catch (Exception ex)
            {
                throw new Exception("Unable to deserialize the configuration." + Environment.NewLine + "Type: " + typeof(T).Name + Environment.NewLine + "Path: " + path, ex);
            }
            finally
            {
                System.Threading.Monitor.Exit(configuration_cache);
            }
        }
    }
}
