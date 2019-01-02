using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using Schalltech.EnterpriseLibrary.IO;

// This custom assembly attribute retrieves the path to the log4net configuration file.
[assembly: Schalltech.EnterpriseLibrary.Logging.XmlConfigurator(Watch = true)]

namespace Schalltech.EnterpriseLibrary.Logging
{
    public class Logger
    {
        static LoggerConfiguration settings = null;
        static object padlock = new object();

        static string[] default_appender_hint_paths = new string[]
        {
            // Server path.
            IO.Path.GetAbsolutePath(@"Configurations\log4net_appenders.config"),
            IO.Path.GetAbsolutePath(@"Configurations\Main\log4net_appenders.config"),

            // Local TFS mapping folder path.            
            IO.Path.GetAbsolutePath(@"..\..\Configurations\Main\log4net_appenders.config")
        };

        static public void Write(object message, string title)
        {
            Write(message, 0, 0, System.Diagnostics.TraceEventType.Information, title, null);
        }

        static public void Write(object message, int priority, int eventid, System.Diagnostics.TraceEventType severity, string title)
        {
            Write(message, priority, eventid, severity, title, null);
        }

        static public void Write(object message, int priority, int eventid, System.Diagnostics.TraceEventType severity, string title, LogCategory logCategory)
        {
            log4net.ILog log = null;
            log4net.Core.IAppenderAttachable appenders = null;

            try
            {
                Monitor.Enter(padlock);

                // Intialize the default settings.
                if (settings == null)
                {
                    try
                    {
                        // Initialize the log settings from the default appender configuration file.
                        IntializeLogSettings();

                        // Custom appenders are optional to support overriding the default configuration.
                        if (System.Configuration.ConfigurationManager.AppSettings["log4net_appenders_custom"] != null)
                        {
                            var custom = EnterpriseLibrary.Configuration.ConfigurationManager.GetConfiguration<LoggerConfiguration>("log4net_appenders_custom");

                            // Override the default settings with the defined custom values.
                            settings = custom.Merge(settings);
                        }

                        // Create the default logger and access its appender collection.
                        log = log4net.LogManager.GetLogger(settings.CategoryName);
                        appenders = (log4net.Core.IAppenderAttachable)log.Logger;

                        if (settings.AppenderCollection != null)
                        {
                            // Create the appenders and add them to the loggers collection.
                            ManageAppenderCollection(settings.AppenderCollection.Appenders, appenders);
                        }
                        else
                        {
                            // No appenders were found. Create the appender(s) based on the defaults. 
                            if (settings.DefaultAppenders != null)
                            {
                                CreateAppender(settings.DefaultAppenders.RollingFileAppender, appenders);
                                CreateAppender(settings.DefaultAppenders.ConsoleAppender, appenders);
                            }
                            else
                            {
                                throw new Exception("A default appender was not found. In order to support logging at least one default logger must be defined.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // We were unable to initialize the settings without errors.
                        settings = null;

                        throw ex;
                    }
                }

                // CUSTOM LOG CATEGORY
                // If a log category is defined use it.
                if (logCategory != null && !string.IsNullOrEmpty(logCategory.Name))
                {
                    // Check to see if the category's logger already exists. If it does then the category's appenders have already been created.
                    if (log4net.LogManager.Exists(logCategory.Name) == null)
                    {
                        // Create the catgory's logger and it's appenders.
                        // GetLogger() will create the logger and add it to the repository if it does not already exist so we
                        // need to see if it exists before calling this method or we will not know that we need to create the appenders.
                        log = log4net.LogManager.GetLogger(logCategory.Name);
                        appenders = (log4net.Core.IAppenderAttachable)log.Logger;

                        // Create the appenders.
                        ManageAppenderCollection(logCategory.Appenders, appenders);
                    }
                    else
                    {
                        // Get the customized logger.
                        log = log4net.LogManager.GetLogger(logCategory.Name);
                    }
                }

                // DEFAULT APPENDER
                // A log category was not provided so the default must be used.
                else
                {
                    // Get the default logger.
                    log = log4net.LogManager.GetLogger(settings.CategoryName);
                }

                // Write the log message to the file.
                switch (severity)
                {
                    case TraceEventType.Information:
                        log.Info(message);
                        break;
                    case TraceEventType.Error:
                        log.Error(message);
                        break;
                    case TraceEventType.Warning:
                        log.Warn(message);
                        break;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                Monitor.Exit(padlock);
            }
        }

        static void IntializeLogSettings()
        {
            // Default appenders are required.
            if (System.Configuration.ConfigurationManager.AppSettings["log4net_appenders"] != null)
            {
                settings = EnterpriseLibrary.Configuration.ConfigurationManager.GetConfiguration<LoggerConfiguration>("log4net_appenders");

                // VERIFY THE REQUIRED DEFAULT SETTINGS ARE DEFINED ON THE ROOT SETTINGS OBJECT...
            }
            else
            {
                // The appsetting referencing the appender configuration was not found. Check the hint paths as a last resort.
                // Attempt to get the path to the configuration file from one of the hint paths.
                string path = IO.Path.CheckHintPaths(default_appender_hint_paths);

                if (!string.IsNullOrEmpty(path))
                {
                    // The configuration was found, load the settings.
                    settings = EnterpriseLibrary.Configuration.ConfigurationManager.LoadConfiguration<LoggerConfiguration>(path);
                    return;
                }
                else
                {
                    // The configuration file could not be located by one of the hint paths.
                    string message = Environment.NewLine +
                    "---------------------------------------------------------- Log4Net Error Encountered ----------------------------------------------------------" + Environment.NewLine +
                    " Error: The key [log4net_appenders] does not exist in the applications appsettings collection." + Environment.NewLine +
                    " This key is required and must reference the log4net appenders configuration defining the default appenders used by the application." + Environment.NewLine + Environment.NewLine +
                    " Remarks: The following hint paths were also searched in an attempt to locate the configuration file." + Environment.NewLine;

                    // Add each hint path checked to the error message.
                    foreach (var hint_path in default_appender_hint_paths)
                    {
                        try
                        {
                            message = message + " " + hint_path + Environment.NewLine;
                        }
                        catch (Exception)
                        { }
                    }

                    message = message + "-----------------------------------------------------------------------------------------------------------------------------------------------";
                    throw new Exception(message);
                }
            }
        }

        static void ManageAppenderCollection(List<AppenderConfiguration> appenders, log4net.Core.IAppenderAttachable collection)
        {
            if (appenders != null)
            {
                foreach (var configuration in appenders)
                {
                    if (configuration is RollingFileAppenderConfiguration)
                        CreateAppender(((RollingFileAppenderConfiguration)configuration).Merge(settings.DefaultAppenders.RollingFileAppender), collection);

                    else if (configuration is ConsoleAppenderConfiguration)
                        CreateAppender(((ConsoleAppenderConfiguration)configuration).Merge(settings.DefaultAppenders.RollingFileAppender), collection);

                    else
                        throw new Exception("Unable to create the appender. " + configuration.GetType().Name + "'s are not supported.");
                }
            }
        }

        static void CreateAppender(AppenderConfiguration configuration, log4net.Core.IAppenderAttachable collection)
        {
            if (configuration != null)
            {
                if (!string.IsNullOrEmpty(configuration.Name))
                {
                    if (collection.GetAppender(configuration.Name) == null)
                    {
                        // Create the appender and add it to the loggers collection of appenders.
                        // Create the appender based on the type of configuration.
                        if (configuration is RollingFileAppenderConfiguration)
                        {
                            var rolling_file_configuration = configuration as RollingFileAppenderConfiguration;

                            // If a file name is not provided for the log file then attempt to use the applications name.
                            var rolling_appender = new log4net.Appender.RollingFileAppender
                            {
                                Name = configuration.Name,
                                MaxSizeRollBackups = Convert.ToInt32(rolling_file_configuration.MaxArchivedFiles),
                                MaximumFileSize = rolling_file_configuration.RollSizeKB,
                                RollingStyle = log4net.Appender.RollingFileAppender.RollingMode.Size,
                                StaticLogFileName = true,
                                LockingModel = new log4net.Appender.RollingFileAppender.MinimalLock(),
                                File = rolling_file_configuration.Path + (string.IsNullOrEmpty(rolling_file_configuration.FileName) ? GetApplicationName() + ".log" : rolling_file_configuration.FileName),
                                PreserveLogFileNameExtension = true,
                                Layout = new log4net.Layout.PatternLayout(rolling_file_configuration.Template),
                            };

                            //rolling_appender.AddFilter(new log4net.Filter.PropertyFilter{ Key="", StringToMatch=""}); //.StringMatchFilter { StringToMatch = category });
                            //rolling_appender.AddFilter(new log4net.Filter.DenyAllFilter());
                            rolling_appender.ActivateOptions();

                            collection.AddAppender(rolling_appender);
                        }
                        else
                        {
                            throw new Exception("Unable to create the appender. " + configuration.GetType().Name + "'s are not supported.");
                        }
                    }
                }
                else
                {
                    throw new Exception("Unable to create the appender. The name property is undefined.");
                }
            }
        }

        static string GetApplicationName()
        {
            string app_name = "";

            if (System.Reflection.Assembly.GetEntryAssembly() == null)
            {
                // Try to detect if we are dealing with IIS...
                string ass_name = Process.GetCurrentProcess().MainModule.FileName;
                if (ass_name.EndsWith("iisexpress.exe") || ass_name.EndsWith("inetinfo.exe") || ass_name.EndsWith("w3wp.exe"))
                {
                    // Attempt to get the name of the web site.
                    app_name = Environment.GetCommandLineArgs()[2].Replace("/site:", "");
                }
                else
                {
                    app_name = "unknown";
                }
            }
            else
            {
                app_name = System.Reflection.Assembly.GetEntryAssembly().GetName().Name;
            }

            return app_name;
        }
    }

    public class LoggerConfiguration
    {
        [XmlIgnore]
        public string CategoryName = "Default";

        [XmlElement("DefaultAppenders")]
        public DefaultAppenders DefaultAppenders { get; set; }

        [XmlElement("Appenders")]
        public LogCategory AppenderCollection { get; set; }

        public LoggerConfiguration Merge(LoggerConfiguration defaultLogger)
        {
            if (DefaultAppenders == null)
            {
                DefaultAppenders = defaultLogger.DefaultAppenders;
            }
            else if (defaultLogger.DefaultAppenders != null)
            {
                // Merge settings for the default rolling file appdender.
                if (defaultLogger.DefaultAppenders.RollingFileAppender != null)
                {
                    if (DefaultAppenders.RollingFileAppender == null)
                        DefaultAppenders.RollingFileAppender = defaultLogger.DefaultAppenders.RollingFileAppender;
                    else
                        DefaultAppenders.RollingFileAppender = DefaultAppenders.RollingFileAppender.Merge(defaultLogger.DefaultAppenders);
                }

                // Merge settings for the default console appdender.
                if (defaultLogger.DefaultAppenders.ConsoleAppender != null)
                {
                    if (DefaultAppenders.ConsoleAppender == null)
                        DefaultAppenders.ConsoleAppender = defaultLogger.DefaultAppenders.ConsoleAppender;
                    else
                        DefaultAppenders.ConsoleAppender = DefaultAppenders.ConsoleAppender.Merge(defaultLogger.DefaultAppenders);
                }
            }

            return this;
        }
    }

    public class DefaultAppenders
    {
        [XmlElement("RollingFileAppender")]
        public RollingFileAppenderConfiguration RollingFileAppender { get; set; }

        [XmlElement("ConsoleAppender")]
        public ConsoleAppenderConfiguration ConsoleAppender { get; set; }
    }

    public class LogCategory
    {
        [XmlAttribute("Name")]
        public string Name { get; set; }

        [XmlElement("Appender")]
        public List<AppenderConfiguration> Appenders = new List<AppenderConfiguration>();

        public AppenderConfiguration Appender(string name)
        {
            if (Appenders != null && Appenders.Count > 0)
            {
                foreach (AppenderConfiguration appender in Appenders)
                {
                    if (appender.Name.ToUpper() == name.ToUpper())
                    {
                        return appender;
                    }
                }
            }

            return null;
        }
    }

    [XmlInclude(typeof(ConsoleAppenderConfiguration))]
    [XmlInclude(typeof(RollingFileAppenderConfiguration))]
    public class AppenderConfiguration
    {
        [XmlAttribute("Name")]
        public string Name { get; set; }

        [XmlAttribute("Enabled")]
        public string Enabled { get; set; }

        [XmlAttribute("Template")]
        public string Template { get; set; }

        public virtual AppenderConfiguration Merge(AppenderConfiguration default_appender)
        {
            if (string.IsNullOrEmpty(Name))
                Name = default_appender.Name;

            if (string.IsNullOrEmpty(Template))
                Template = default_appender.Template;

            return this;
        }
    }

    public class ConsoleAppenderConfiguration : AppenderConfiguration
    {
        public ConsoleAppenderConfiguration Merge(DefaultAppenders appenders)
        {
            try
            {
                var default_appender = appenders.ConsoleAppender;

                if (default_appender != null)
                {
                    base.Merge(default_appender);
                }

                return this;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }

    public class RollingFileAppenderConfiguration : AppenderConfiguration
    {
        [XmlAttribute("Path")]
        public string Path { get; set; }

        [XmlAttribute("FileName")]
        public string FileName { get; set; }

        [XmlAttribute("RollFileExistsBehavior")]
        public string RollFileExistsBehavior { get; set; }

        [XmlAttribute("RollInterval")]
        public string RollInterval { get; set; }

        [XmlAttribute("RollSizeKB")]
        public string RollSizeKB { get; set; }

        [XmlAttribute("TimeStampPattern")]
        public string TimeStampPattern { get; set; }

        [XmlAttribute("MaxArchivedFiles")]
        public string MaxArchivedFiles { get; set; }

        public RollingFileAppenderConfiguration Merge(DefaultAppenders appenders)
        {
            try
            {
                var default_appender = appenders.RollingFileAppender;

                if (default_appender != null)
                {
                    base.Merge(default_appender);

                    if (string.IsNullOrEmpty(Path))
                        Path = default_appender.Path;

                    if (string.IsNullOrEmpty(FileName))
                    {
                        if (!string.IsNullOrEmpty(default_appender.FileName))
                            FileName = default_appender.FileName;
                        else
                            FileName = System.Reflection.Assembly.GetEntryAssembly().GetName().Name + ".log";
                    }

                    if (string.IsNullOrEmpty(RollFileExistsBehavior))
                        RollFileExistsBehavior = default_appender.RollFileExistsBehavior;

                    if (string.IsNullOrEmpty(RollInterval))
                        RollInterval = default_appender.RollInterval;

                    if (string.IsNullOrEmpty(RollSizeKB))
                        RollSizeKB = default_appender.RollSizeKB;

                    if (string.IsNullOrEmpty(TimeStampPattern))
                        TimeStampPattern = default_appender.TimeStampPattern;

                    if (string.IsNullOrEmpty(MaxArchivedFiles))
                        MaxArchivedFiles = default_appender.MaxArchivedFiles;
                }

                return this;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public RollingFileAppenderConfiguration Merge(RollingFileAppenderConfiguration default_appender)
        {
            try
            {
                if (default_appender != null)
                {
                    base.Merge(default_appender);

                    if (string.IsNullOrEmpty(Path))
                        Path = default_appender.Path;

                    if (string.IsNullOrEmpty(FileName))
                    {
                        if (!string.IsNullOrEmpty(default_appender.FileName))
                            FileName = default_appender.FileName;
                        else
                            FileName = System.Reflection.Assembly.GetEntryAssembly().GetName().Name + ".log";
                    }

                    if (string.IsNullOrEmpty(RollFileExistsBehavior))
                        RollFileExistsBehavior = default_appender.RollFileExistsBehavior;

                    if (string.IsNullOrEmpty(RollInterval))
                        RollInterval = default_appender.RollInterval;

                    if (string.IsNullOrEmpty(RollSizeKB))
                        RollSizeKB = default_appender.RollSizeKB;

                    if (string.IsNullOrEmpty(TimeStampPattern))
                        TimeStampPattern = default_appender.TimeStampPattern;

                    if (string.IsNullOrEmpty(MaxArchivedFiles))
                        MaxArchivedFiles = default_appender.MaxArchivedFiles;
                }

                return this;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }

    public class XmlConfigurator : log4net.Config.XmlConfiguratorAttribute
    {
        public XmlConfigurator()
        {
            string[] hint_paths = null;

            // Retrieve the path to the log4net configuration file from the calling applications appsettings collection.
            string path = System.Configuration.ConfigurationManager.AppSettings["log4net"];

            if (!string.IsNullOrEmpty(path))
            {
                // Store the path to the configuration file.
                ConfigFile = path;
            }
            else
            {
                hint_paths = new string[]
                { 
                    // Server path.
                    IO.Path.GetAbsolutePath(@"Configurations\log4net.config"),
                    IO.Path.GetAbsolutePath(@"Configurations\Main\log4net.config"),

                    // Local TFS mapping folder path.
                    IO.Path.GetAbsolutePath(@"..\..\Configurations\Main\log4net.config")
                };

                // Attempt to get the path to the configuration file from one of the hint paths.
                path = IO.Path.CheckHintPaths(hint_paths);

                if (!string.IsNullOrEmpty(path))
                {
                    // The configuration was found.
                    ConfigFile = path;
                }
                else
                {
                    string message = Environment.NewLine +
                    "---------------------------------------------------------- Log4Net Error Encountered ----------------------------------------------------------" + Environment.NewLine +
                    " Error: The [log4net] application setting was not found." + Environment.NewLine +
                    " This key must be defined and reference the log4net configuration file in order to support logging." + Environment.NewLine + Environment.NewLine +
                    " Remarks: The following hint paths were also searched in an attempt to locate the configuration file." + Environment.NewLine;

                    foreach (var hint_path in hint_paths)
                    {
                        try
                        {
                            message = message + " " + hint_path + Environment.NewLine;
                        }
                        catch (Exception)
                        { }
                    }

                    message = message + "-----------------------------------------------------------------------------------------------------------------------------------------------";
                    throw new Exception(message);
                }
            }
        }
    }
}
