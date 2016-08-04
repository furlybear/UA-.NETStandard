/* ========================================================================
 * Copyright (c) 2005-2016 The OPC Foundation, Inc. All rights reserved.
 *
 * OPC Foundation MIT License 1.00
 * 
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following
 * conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 *
 * The complete license agreement can be found here:
 * http://opcfoundation.org/License/MIT/1.00/
 * ======================================================================*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Net;

namespace Opc.Ua.Configuration
{
    public abstract class IApplicationMessageDlg
    {
        public abstract void Message(string text, Boolean ask=false);
        public abstract Task<bool> ShowAsync();
    }

    /// <summary>
    /// A class that install, configures and runs a UA application.
    /// </summary>
    public class ApplicationInstance
    {
        #region Ctors
        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationInstance"/> class.
        /// </summary>
        public ApplicationInstance()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationInstance"/> class.
        /// </summary>
        /// <param name="applicationConfiguration">The application configuration.</param>
        public ApplicationInstance(ApplicationConfiguration applicationConfiguration)
        {
            m_applicationConfiguration = applicationConfiguration;
        } 
        #endregion

        #region Public Properties
        /// <summary>
        /// Gets or sets the name of the application.
        /// </summary>
        /// <value>The name of the application.</value>
        public string ApplicationName
        {
            get { return m_applicationName; }
            set { m_applicationName = value; }
        }

        /// <summary>
        /// Gets or sets the type of the application.
        /// </summary>
        /// <value>The type of the application.</value>
        public ApplicationType ApplicationType
        {
            get { return m_applicationType; }
            set { m_applicationType = value; }
        }

        /// <summary>
        /// Gets or sets the name of the config section containing the path to the application configuration file.
        /// </summary>
        /// <value>The name of the config section.</value>
        public string ConfigSectionName
        {
            get { return m_configSectionName; }
            set { m_configSectionName = value; }
        }

        /// <summary>
        /// Gets or sets the type of configuration file.
        /// </summary>
        /// <value>The type of configuration file.</value>
        public Type ConfigurationType
        {
            get { return m_configurationType; }
            set { m_configurationType = value; }
        }

        /// <summary>
        /// Gets or sets the installation configuration.
        /// </summary>
        /// <value>The installation configuration.</value>
        public InstalledApplication InstallConfig
        {
            get { return m_installConfig; }
            set { m_installConfig = value; }
        }

        /// <summary>
        /// Gets the server.
        /// </summary>
        /// <value>The server.</value>
        public ServerBase Server
        {
            get { return m_server; }
        }

        /// <summary>
        /// Gets the application configuration used when the Start() method was called.
        /// </summary>
        /// <value>The application configuration.</value>
        public ApplicationConfiguration ApplicationConfiguration
        {
            get { return m_applicationConfiguration; }
            set { m_applicationConfiguration = value; }
        }

        /// <summary>
        /// Gets or sets a flag that indicates whether the application will be set up for management with the GDS agent.
        /// </summary>
        /// <value>If true the application will not be visible to the GDS local agent after installation.</value>
        public bool NoGdsAgentAdmin { get; set; }

        public static IApplicationMessageDlg MessageDlg { get; set; }
        #endregion

        #region InstallConfig Handling
        /// <summary>
        /// Loads the installation configuration from a file.
        /// </summary>
        public InstalledApplication LoadInstallConfigFromFile(string filePath)
        {
            if (filePath == null) throw new ArgumentNullException("filePath");
            
            Stream istrm = null;

            try
            {
                istrm = File.Open(filePath, FileMode.Open, FileAccess.Read);
            }
            catch (Exception e)
            {
                throw ServiceResultException.Create(StatusCodes.BadDecodingError, e, "Could not open file: {0}", filePath);
            }

            return LoadInstallConfigFromStream(istrm);
        }

        /// <summary>
        /// Loads the installation configuration from an embedded resource.
        /// </summary>
        public InstalledApplication LoadInstallConfigFromResource(string resourcePath, Assembly assembly)
        {
            if (resourcePath == null) throw new ArgumentNullException("resourcePath");

            Stream istrm = assembly.GetManifestResourceStream(resourcePath);

            if (istrm == null)
            {
                throw ServiceResultException.Create(StatusCodes.BadDecodingError, "Could not find resource file: {0}", resourcePath);
            }

            return LoadInstallConfigFromStream(istrm);
        }

        /// <summary>
        /// Loads the installation configuration from a stream.
        /// </summary>
        public InstalledApplication LoadInstallConfigFromStream(Stream istrm)
        {
            try
            {
                DataContractSerializer serializer = new DataContractSerializer(typeof(InstalledApplication));
                return (InstalledApplication)serializer.ReadObject(istrm);
                
            }
            catch (Exception e)
            {
                throw ServiceResultException.Create(StatusCodes.BadDecodingError, e, "Could not parse install configuration.");
            }
        }

        /// <summary>
        /// Loads the installation configuration.
        /// </summary>
        /// <param name="configFile">The config file (may be null).</param>
        public virtual void LoadInstallConfig(string configFile)
        {
            // load configuration from command line.
            if (!String.IsNullOrEmpty(configFile))
            {
                InstallConfig = LoadInstallConfigFromFile(configFile);
            }

            // load it from a resource if not already loaded.
            else if (InstallConfig == null)
            {
                foreach (string resourcePath in this.GetType().GetTypeInfo().Assembly.GetManifestResourceNames())
                {
                    if (resourcePath.EndsWith("InstallConfig.xml"))
                    {
                        InstallConfig = LoadInstallConfigFromResource(resourcePath, this.GetType().GetTypeInfo().Assembly);
                        break;
                    }
                }

                if (InstallConfig == null)
                {
                    throw new ServiceResultException(StatusCodes.BadConfigurationError, "Could not load default installation config file.");
                }
            }

            // override the application name.
            if (String.IsNullOrEmpty(InstallConfig.ApplicationName))
            {
                InstallConfig.ApplicationName = ApplicationName;
            }
            else
            {
                ApplicationName = InstallConfig.ApplicationName;
            }

            // update fixed fields in the installation config.
            InstallConfig.ApplicationType = (Opc.Ua.Security.ApplicationType)(int)ApplicationType;
            InstallConfig.ExecutableFile = Directory.GetCurrentDirectory();

            if (InstallConfig.TraceConfiguration != null)
            {
                InstallConfig.TraceConfiguration.ApplySettings();
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Starts the UA server.
        /// </summary>
        /// <param name="server">The server.</param>
        public async Task Start(ServerBase server)
        {
            m_server = server;

            if (m_applicationConfiguration == null)
            {
                await LoadApplicationConfiguration(false);
            }

            if (m_applicationConfiguration.SecurityConfiguration != null && m_applicationConfiguration.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                m_applicationConfiguration.CertificateValidator.CertificateValidation += CertificateValidator_CertificateValidation;
            }

            server.Start(m_applicationConfiguration);
        }

        /// <summary>
        /// Stops the UA server.
        /// </summary>
        public void Stop()
        {
            m_server.Stop();
        }
        #endregion

        #region WindowsService Class
        /// <summary>
        /// Manages the interface between the UA server and the Windows SCM.
        /// </summary>
        protected class WindowsService
        {
            #region Constructors
            /// <summary>
            /// Initializes a new instance of the <see cref="WindowsService"/> class.
            /// </summary>
            /// <param name="server">The server.</param>
            /// <param name="configSectionName">Name of the config section.</param>
            /// <param name="applicationType">Type of the application.</param>
            /// <param name="configurationType">Type of the configuration.</param>
            public WindowsService(ServerBase server, string configSectionName, ApplicationType applicationType, Type configurationType)
            {
                m_server = server;
                m_configSectionName = configSectionName;
                m_applicationType = applicationType;
                m_configurationType = configurationType;
            }
            #endregion

            #region Private Methods
            /// <summary>
            /// Runs the service in a background thread.
            /// </summary>
            private async Task OnBackgroundStart(object state)
            {
                string filePath = null;
                ApplicationConfiguration configuration = null;

                try
                {
                    filePath = ApplicationConfiguration.GetFilePathFromAppConfig(m_configSectionName);
                    configuration = await ApplicationInstance.LoadAppConfig(false, filePath, m_applicationType, m_configurationType, true);
                }
                catch (Exception e)
                {
                    ServiceResult error = ServiceResult.Create(e, StatusCodes.BadConfigurationError, "Could not load UA Service configuration file.\r\nPATH={0}", filePath);
                }

                try
                {
                    if (configuration.SecurityConfiguration != null && configuration.SecurityConfiguration.AutoAcceptUntrustedCertificates)
                    {
                        configuration.CertificateValidator.CertificateValidation += CertificateValidator_CertificateValidation;
                    }

                    m_server.Start(configuration);
                }
                catch (Exception e)
                {
                    ServiceResult error = ServiceResult.Create(e, StatusCodes.BadConfigurationError, "Could not start UA Service.");
                    Utils.Trace((int)Utils.TraceMasks.Error, error.ToLongString());
                }
            }

            #endregion

            #region Private Fields
            private ServerBase m_server;
            private string m_configSectionName;
            private ApplicationType m_applicationType;
            private Type m_configurationType;
            #endregion
        }
        #endregion

        #region ArgumentDescription Class
        /// <summary>
        /// Stores the description of an argument.
        /// </summary>
        protected class ArgumentDescription
        {
            /// <summary>
            /// The argument name.
            /// </summary>
            public string Name;

            /// <summary>
            /// The argument description.
            /// </summary>
            public string Description;

            /// <summary>
            /// Whether the argument requires a value.
            /// </summary>
            public bool ValueRequired;

            /// <summary>
            /// Whether the argument allows a value.
            /// </summary>
            public bool ValueAllowed;

            /// <summary>
            /// Initializes a new instance of the <see cref="ArgumentDescription"/> class.
            /// </summary>
            /// <param name="name">The name.</param>
            /// <param name="valueRequired">if set to <c>true</c> a value is required.</param>
            /// <param name="valueAllowed">if set to <c>true</c> a value is allowed.</param>
            /// <param name="description">The description.</param>
            public ArgumentDescription(
                 string name,
                 bool valueRequired,
                 bool valueAllowed,
                 string description)
            {
                Name = name;
                ValueRequired = valueRequired;
                ValueAllowed = valueAllowed;
                Description = description;
            }
        }

        private static ArgumentDescription[] s_SupportedArguments = new ArgumentDescription[]
        {            
            new ArgumentDescription("/start", false, false, "Starts the application as a service (/start [/silent] [/configFile:<filepath>])."),
            new ArgumentDescription("/install", false, false, "Installs the application (/install [/silent] [/configFile:<filepath>])."),
            new ArgumentDescription("/uninstall", false, false, "Uninstalls the application (/uninstall [/silent] [/configFile:<filepath>])."),
            new ArgumentDescription("/silent", false, false, "Performs operations without prompting user to confirm or displaying errors."),
            new ArgumentDescription("/configFile", true, true, "Specifies the installation configuration file."),
        };
        #endregion

        #region Protected Methods
        /// <summary>
        /// Gets the descriptions for the supported arguments.
        /// </summary>
        protected virtual ArgumentDescription[] GetArgumentDescriptions()
        {
            return s_SupportedArguments;
        }

        /// <summary>
        /// Gets the help string.
        /// </summary>
        protected virtual string GetHelpString(ArgumentDescription[] commands)
        {
            StringBuilder text = new StringBuilder();
            text.Append("These are the supported arguments:\r\n");

            for (int ii = 0; ii < commands.Length; ii++)
            {
                ArgumentDescription command = commands[ii];

                text.Append("\r\n");

                if (command.ValueRequired)
                {
                    text.AppendFormat("{0}:<value> {1}", command.Name, command.Description);
                }
                else if (command.ValueAllowed)
                {
                    text.AppendFormat("{0}[:<value>] {1}", command.Name, command.Description);
                }
                else
                {
                    text.AppendFormat("{0} {1}", command.Name, command.Description);
                }
            }

            text.Append("\r\n");
            return text.ToString();
        }

        /// <summary>
        /// Validates the arguments.
        /// </summary>
        protected virtual string ValidateArguments(bool ignoreUnknownArguments, Dictionary<string, string> args)
        {
            ArgumentDescription[] commands = GetArgumentDescriptions();

            // check if help was requested.
            if (args.ContainsKey("/?"))
            {
                return GetHelpString(commands);
            }

            // validate the arguments.
            StringBuilder error = new StringBuilder();

            foreach (KeyValuePair<string,string> arg in args)
            {
                ArgumentDescription command = null;

                for (int ii = 0; ii < commands.Length; ii++)
                {
                    if (String.Compare(commands[ii].Name, arg.Key, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        command = commands[ii];
                        break;
                    }
                }

                if (command == null)
                {
                    if (!ignoreUnknownArguments)
                    {
                        if (error.Length > 0)
                        {
                            error.Append("\r\n");
                        }

                        error.AppendFormat("Unrecognized argument: {0}", arg.Key);
                    }

                    continue;
                }

                if (command.ValueRequired && String.IsNullOrEmpty(arg.Value))
                {
                    if (error.Length > 0)
                    {
                        error.Append("\r\n");
                    }

                    error.AppendFormat("{0} requires a value to be specified (syntax {0}:<value>).", arg.Key);
                    continue;
                }

                if (!command.ValueAllowed && !String.IsNullOrEmpty(arg.Value))
                {
                    if (error.Length > 0)
                    {
                        error.Append("\r\n");
                    }

                    error.AppendFormat("{0} does not allow a value to be specified.", arg.Key);
                    continue;
                }
            }

            // return any error text.
            return error.ToString();
        }

        /// <summary>
        /// Updates the application configuration with the values from the installation configuration.
        /// </summary>
        /// <param name="configuration">The configuration to update.</param>
        protected virtual async Task UpdateAppConfigWithInstallConfig(ApplicationConfiguration configuration)
        {
            // override the application name.
            if (InstallConfig.ApplicationName != null)
            {
                if (configuration.SecurityConfiguration != null && configuration.SecurityConfiguration.ApplicationCertificate != null)
                {
                    if (configuration.SecurityConfiguration.ApplicationCertificate.SubjectName == configuration.ApplicationName)
                    {
                        configuration.SecurityConfiguration.ApplicationCertificate.SubjectName = InstallConfig.ApplicationName;
                    }
                }

                configuration.ApplicationName = InstallConfig.ApplicationName;
            }

            if (InstallConfig.ApplicationUri != null)
            {
                configuration.ApplicationUri = InstallConfig.ApplicationUri;
            }

            // replace localhost with the current machine name.
            if (configuration.ApplicationUri != null)
            {
                int index = configuration.ApplicationUri.IndexOf("localhost", StringComparison.OrdinalIgnoreCase);

                if (index != -1)
                {
                    StringBuilder buffer = new StringBuilder();
                    buffer.Append(configuration.ApplicationUri.Substring(0, index));
                    buffer.Append(Utils.GetHostName());
                    buffer.Append(configuration.ApplicationUri.Substring(index + "localhost".Length));
                    configuration.ApplicationUri = buffer.ToString();
                }
            }

            ServerBaseConfiguration serverConfiguration = null;

            if (configuration.ServerConfiguration != null)
            {
                serverConfiguration = configuration.ServerConfiguration;
            }
            else if (configuration.DiscoveryServerConfiguration != null)
            {
                serverConfiguration = configuration.DiscoveryServerConfiguration;
            }

            if (serverConfiguration != null)
            {
                if (InstallConfig.BaseAddresses != null && InstallConfig.BaseAddresses.Count > 0)
                {
                    Dictionary<string, string> addresses = new Dictionary<string, string>();
                    serverConfiguration.BaseAddresses.Clear();

                    for (int ii = 0; ii < InstallConfig.BaseAddresses.Count; ii++)
                    {
                        Uri url = Utils.ParseUri(InstallConfig.BaseAddresses[ii]);

                        if (url != null)
                        {
                            if (!addresses.ContainsKey(url.Scheme))
                            {
                                serverConfiguration.BaseAddresses.Add(url.ToString());
                                addresses.Add(url.Scheme, String.Empty);
                            }
                            else
                            {
                                serverConfiguration.AlternateBaseAddresses.Add(url.ToString());
                            }
                        }
                    }
                }

                if (InstallConfig.SecurityProfiles != null && InstallConfig.SecurityProfiles.Count > 0)
                {
                    ServerSecurityPolicyCollection securityPolicies = new ServerSecurityPolicyCollection();

                    for (int ii = 0; ii < InstallConfig.SecurityProfiles.Count; ii++)
                    {
                        for (int jj = 0; jj < serverConfiguration.SecurityPolicies.Count; jj++)
                        {
                            if (serverConfiguration.SecurityPolicies[jj].SecurityPolicyUri == InstallConfig.SecurityProfiles[ii].ProfileUri)
                            {
                                securityPolicies.Add(serverConfiguration.SecurityPolicies[jj]);
                            }
                        }
                    }

                    serverConfiguration.SecurityPolicies = securityPolicies;
                }
            }

            if (InstallConfig.ApplicationCertificate != null)
            {
                configuration.SecurityConfiguration.ApplicationCertificate.StoreType = InstallConfig.ApplicationCertificate.StoreType;
                configuration.SecurityConfiguration.ApplicationCertificate.StorePath = InstallConfig.ApplicationCertificate.StorePath;

                if (String.IsNullOrEmpty(InstallConfig.ApplicationCertificate.SubjectName))
                {
                    configuration.SecurityConfiguration.ApplicationCertificate.SubjectName = InstallConfig.ApplicationCertificate.SubjectName;
                }
            }

            if (InstallConfig.RejectedCertificatesStore != null)
            {
                configuration.SecurityConfiguration.RejectedCertificateStore = Opc.Ua.Security.SecuredApplication.FromCertificateStoreIdentifier(InstallConfig.RejectedCertificatesStore);
            }

            if (InstallConfig.IssuerCertificateStore != null)
            {
                configuration.SecurityConfiguration.TrustedIssuerCertificates.StoreType = InstallConfig.IssuerCertificateStore.StoreType;
                configuration.SecurityConfiguration.TrustedIssuerCertificates.StorePath = InstallConfig.IssuerCertificateStore.StorePath;
                configuration.SecurityConfiguration.TrustedIssuerCertificates.ValidationOptions = (CertificateValidationOptions)(int)InstallConfig.IssuerCertificateStore.ValidationOptions;
            }

            if (InstallConfig.TrustedCertificateStore != null)
            {
                configuration.SecurityConfiguration.TrustedPeerCertificates.StoreType = InstallConfig.TrustedCertificateStore.StoreType;
                configuration.SecurityConfiguration.TrustedPeerCertificates.StorePath = InstallConfig.TrustedCertificateStore.StorePath;
                configuration.SecurityConfiguration.TrustedPeerCertificates.ValidationOptions = (CertificateValidationOptions)(int)InstallConfig.TrustedCertificateStore.ValidationOptions;
            }

            await configuration.CertificateValidator.Update(configuration);
        }

        /// <summary>
        /// Installs the service.
        /// </summary>
        /// <param name="silent">if set to <c>true</c> no dialogs such be displayed.</param>
        /// <param name="args">Additional arguments provided on the command line.</param>
        protected virtual async Task Install(bool silent, Dictionary<string, string> args)
        {
            Utils.Trace(Utils.TraceMasks.Information, "Installing application.");

            // check the configuration.
            string filePath = Utils.GetAbsoluteFilePath(InstallConfig.ConfigurationFile, true, false, false);

            if (filePath == null)
            {
                Utils.Trace("WARNING: Could not load config file specified in the installation configuration: {0}", InstallConfig.ConfigurationFile);
                filePath = ApplicationConfiguration.GetFilePathFromAppConfig(ConfigSectionName);
                InstallConfig.ConfigurationFile = filePath;
            }

            ApplicationConfiguration configuration = await LoadAppConfig(silent, filePath, Opc.Ua.Security.SecuredApplication.FromApplicationType(InstallConfig.ApplicationType), ConfigurationType, false);

            if (configuration == null)
            {
                return;
            }

            // update the configuration.
            await UpdateAppConfigWithInstallConfig(configuration);
            ApplicationConfiguration = configuration;

            // update configuration with information form the install config.
            // check the certificate.
            X509Certificate2 certificate = await configuration.SecurityConfiguration.ApplicationCertificate.Find(true);

            if (certificate != null)
            {
                if (!silent)
                {
                    bool result = await CheckApplicationInstanceCertificate(configuration, certificate, silent, InstallConfig.MinimumKeySize);
                    if (!result)
                    {
                        certificate = null;
                    }
                }
            }

            // ensure the certificate is trusted.
            await AddToTrustedStore(configuration, certificate);

            // add to discovery server.
            if (configuration.ApplicationType == ApplicationType.Server || configuration.ApplicationType == ApplicationType.ClientAndServer)
            {
                try
                {
                    await AddToDiscoveryServerTrustList(certificate, null, null, configuration.SecurityConfiguration.TrustedPeerCertificates);
                }
                catch (Exception e)
                {
                    Utils.Trace(e, "Could not add certificate to LDS trust list.");
                }
            }

            // configure access to the executable, the configuration file and the private key. 
            await ConfigureFileAccess(configuration);

            // update configuration file.
            ConfigUtils.UpdateConfigurationLocation(InstallConfig.ExecutableFile, InstallConfig.ConfigurationFile);

            try
            {
                // ensure the RawData does not get serialized.
                certificate = configuration.SecurityConfiguration.ApplicationCertificate.Certificate;

                configuration.SecurityConfiguration.ApplicationCertificate.Certificate = null;
                configuration.SecurityConfiguration.ApplicationCertificate.SubjectName = certificate.Subject;
                configuration.SecurityConfiguration.ApplicationCertificate.Thumbprint = certificate.Thumbprint;

                configuration.SaveToFile(configuration.SourceFilePath);

                // restore the configuration.
                configuration.SecurityConfiguration.ApplicationCertificate.Certificate = certificate;
            }
            catch (Exception e)
            {
                Utils.Trace(e, "Could not save configuration file. FilePath={0}", configuration.SourceFilePath);
            }

            if (!NoGdsAgentAdmin)
            {
                try
                {
                    // install the GDS agent configuration file
                    string agentPath = Utils.GetAbsoluteDirectoryPath(
                        Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "OPC Foundation" + Path.DirectorySeparatorChar + "GDS" + Path.DirectorySeparatorChar + "Applications",
                        false,
                        false,
                        true);

                    if (agentPath != null)
                    {
                        Opc.Ua.Security.SecuredApplication export = new Opc.Ua.Security.SecurityConfigurationManager().ReadConfiguration(configuration.SourceFilePath);
                        export.ExecutableFile = InstallConfig.ExecutableFile;

                        DataContractSerializer serializer = new DataContractSerializer(typeof(Opc.Ua.Security.SecuredApplication));

                        using (FileStream ostrm = File.Open(agentPath + Path.DirectorySeparatorChar + configuration.ApplicationName + ".xml", FileMode.Create))
                        {
                            serializer.WriteObject(ostrm, export);
                            Utils.Trace(Utils.TraceMasks.Information, "Created GDS agent configuration file.");
                        }
                    }
                }
                catch (Exception e)
                {
                    Utils.Trace(Utils.TraceMasks.Error, "Could not create GDS agent configuration file: {0}", e.Message);
                }
            }
        }

        /// <summary>
        /// Uninstalls the service.
        /// </summary>
        /// <param name="silent">if set to <c>true</c> no dialogs such be displayed.</param>
        /// <param name="args">Additional arguments provided on the command line.</param>
        protected virtual async Task Uninstall(bool silent, Dictionary<string, string> args)
        {
            // check the configuration.
            string filePath = Utils.GetAbsoluteFilePath(InstallConfig.ConfigurationFile, true, false, false);

            if (filePath == null)
            {
                Utils.Trace("WARNING: Could not load config file specified in the installation configuration: {0}", InstallConfig.ConfigurationFile);
                filePath = ApplicationConfiguration.GetFilePathFromAppConfig(ConfigSectionName);
                InstallConfig.ConfigurationFile = filePath;
            }

            ApplicationConfiguration configuration = await LoadAppConfig(silent, filePath, Opc.Ua.Security.SecuredApplication.FromApplicationType(InstallConfig.ApplicationType), ConfigurationType, false);
            ApplicationConfiguration = configuration;

            if (!NoGdsAgentAdmin)
            {
                try
                {
                    string agentPath = Utils.GetAbsoluteDirectoryPath(
                        Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "OPC Foundation" + Path.DirectorySeparatorChar + "GDS" + Path.DirectorySeparatorChar + "Applications",
                        false,
                        false,
                        false);

                    if (agentPath != null)
                    {
                        File.Delete(agentPath + Path.DirectorySeparatorChar + configuration.ApplicationName + ".xml");
                    }
                }
                catch (Exception e)
                {
                    Utils.Trace(Utils.TraceMasks.Error, "Could not create GDS agent configuration file: {0}", e.Message);
                }
            }
        }
        #endregion

        #region Static Methods
        /// <summary>
        /// Loads the configuration.
        /// </summary>
        public static async Task<ApplicationConfiguration> LoadAppConfig(
            bool silent,
            string filePath,
            ApplicationType applicationType,
            Type configurationType,
            bool applyTraceSettings)
        {
            Utils.Trace(Utils.TraceMasks.Information, "Loading application configuration file. {0}", filePath);

            try
            {
                // load the configuration file.
                ApplicationConfiguration configuration = await ApplicationConfiguration.Load(
                    new System.IO.FileInfo(filePath),
                    applicationType,
                    configurationType,
                    applyTraceSettings);

                if (configuration == null)
                {
                    return null;
                }

                return configuration;
            }
            catch (Exception e)
            {
                // warn user.
                if (!silent && MessageDlg != null)
                {
                    MessageDlg.Message("Load Application Configuration: " + e.Message);
                    await MessageDlg.ShowAsync();
                }

                Utils.Trace(e, "Could not load configuration file. {0}", filePath);
                return null;
            }
        }

        /// <summary>
        /// Loads the application configuration.
        /// </summary>
        public async Task<ApplicationConfiguration> LoadApplicationConfiguration(string filePath, bool silent)
        {
            ApplicationConfiguration configuration = await LoadAppConfig(silent, filePath, ApplicationType, ConfigurationType, true);

            if (configuration == null)
            {
                throw ServiceResultException.Create(StatusCodes.BadConfigurationError, "Could not load configuration file.");
            }

            m_applicationConfiguration = configuration;

            return configuration;
        }

        /// <summary>
        /// Loads the application configuration.
        /// </summary>
        public async Task<ApplicationConfiguration> LoadApplicationConfiguration(bool silent)
        {
            string filePath = ApplicationConfiguration.GetFilePathFromAppConfig(ConfigSectionName);
            ApplicationConfiguration configuration = await LoadAppConfig(silent, filePath, ApplicationType, ConfigurationType, true);

            if (configuration == null)
            {
                throw ServiceResultException.Create(StatusCodes.BadConfigurationError, "Could not load configuration file.");
            }

            m_applicationConfiguration = configuration;

            return configuration;
        }

        /// <summary>
        /// Checks for a valid application instance certificate.
        /// </summary>
        /// <param name="silent">if set to <c>true</c> no dialogs will be displayed.</param>
        /// <param name="minimumKeySize">Minimum size of the key.</param>
        public async Task<bool> CheckApplicationInstanceCertificate(
            bool silent,
            ushort minimumKeySize)
        {
            Utils.Trace(Utils.TraceMasks.Information, "Checking application instance certificate.");

            ApplicationConfiguration configuration = null;

            if (m_applicationConfiguration == null)
            {
                await LoadApplicationConfiguration(silent);
            }

            configuration = m_applicationConfiguration;
            bool certificateValid = false;

            // find the existing certificate.
            CertificateIdentifier id = configuration.SecurityConfiguration.ApplicationCertificate;

            if (id == null)
            {
                throw ServiceResultException.Create(StatusCodes.BadConfigurationError, "Configuration file does not specify a certificate.");
            }

            X509Certificate2 certificate = await id.Find(true);

            // check that it is ok.
            if (certificate != null)
            {
                certificateValid = await CheckApplicationInstanceCertificate(configuration, certificate, silent, minimumKeySize);
            }
            else
            {
                // check for missing private key.
                certificate = await id.Find(false);

                if (certificate != null)
                {
                    throw ServiceResultException.Create(StatusCodes.BadConfigurationError, "Cannot access certificate private key. Subject={0}", certificate.Subject);
                }

                // check for missing thumbprint.
                if (!String.IsNullOrEmpty(id.Thumbprint))
                {
                    if (!String.IsNullOrEmpty(id.SubjectName))
                    {
                        CertificateIdentifier id2 = new CertificateIdentifier();
                        id2.StoreType = id.StoreType;
                        id2.StorePath = id.StorePath;
                        id2.SubjectName = id.SubjectName;

                        certificate = await id2.Find(true);
                    }

                    if (certificate != null)
                    {
                        string message = Utils.Format(
                            "Thumbprint was explicitly specified in the configuration." +
                            "\r\nAnother certificate with the same subject name was found." +
                            "\r\nUse it instead?\r\n" +
                            "\r\nRequested: {0}" +
                            "\r\nFound: {1}",
                            id.SubjectName,
                            certificate.Subject);

                        throw ServiceResultException.Create(StatusCodes.BadConfigurationError, message);
                    }
                    else
                    {
                        string message = Utils.Format("Thumbprint was explicitly specified in the configuration. Cannot generate a new certificate.");
                        throw ServiceResultException.Create(StatusCodes.BadConfigurationError, message);
                    }
                }
            }
                       
            if ((certificate == null) || !certificateValid)
            {
                string message = Utils.Format(
                    "There is no cert with subject {0} in the configuration." +
                    "\r\n Please generate a cert for your application," +
                    "\r\n for example using the provided scripts in the sample" +
                    "\r\n application's project directory, or OpenSSL, or the" +
                    "\r\n OPC Foundation's certificate generator." +
                    "\r\n Then copy the new cert to this location:" +
                    "\r\n{1}",
                    id.SubjectName,
                    id.StorePath);
                throw ServiceResultException.Create(StatusCodes.BadConfigurationError, message);
            }

            // ensure it is trusted.
            await AddToTrustedStore(configuration, certificate);
            

            // add to discovery server.
            if (configuration.ApplicationType == ApplicationType.Server || configuration.ApplicationType == ApplicationType.ClientAndServer)
            {
                try
                {
                    await AddToDiscoveryServerTrustList(certificate, null, null, configuration.SecurityConfiguration.TrustedPeerCertificates);
                }
                catch (Exception e)
                {
                    Utils.Trace(e, "Could not add certificate to LDS trust list.");
                }
            }

            return true;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Handles a certificate validation error.
        /// </summary>
        private static void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            try
            {
                if (e.Error != null && e.Error.Code == StatusCodes.BadCertificateUntrusted)
                {
                    e.Accept = true;
                    Utils.Trace((int)Utils.TraceMasks.Security, "Automatically accepted certificate: {0}", e.Certificate.Subject);
                }
            }
            catch (Exception exception)
            {
                Utils.Trace(exception, "Error accepting certificate.");
            }
        }

        /// <summary>
        /// Creates an application instance certificate if one does not already exist.
        /// </summary>
        private static async Task<bool> CheckApplicationInstanceCertificate(
            ApplicationConfiguration configuration,
            X509Certificate2 certificate,
            bool silent,
            ushort minimumKeySize)
        {
            if (certificate == null)
            {
                return false;
            }

            Utils.Trace(Utils.TraceMasks.Information, "Checking application instance certificate. {0}", certificate.Subject);

            // validate certificate.
            configuration.CertificateValidator.Validate(certificate);

            // check key size.
            if (minimumKeySize > certificate.GetRSAPublicKey().KeySize)
            {
                bool valid = false;

                string message = Utils.Format(
                    "The key size ({0}) in the certificate is less than the minimum provided ({1}). Update certificate?",
                    certificate.GetRSAPublicKey().KeySize,
                    minimumKeySize);

                if (!silent && MessageDlg!=null)
                {
                    MessageDlg.Message(message, true);
                    if (!await MessageDlg.ShowAsync())
                    {
                        valid = true;
                    }
                }

                Utils.Trace(message);

                if (!valid)
                {
                    return false;
                }
            }

            // check domains.
            if (configuration.ApplicationType != ApplicationType.Client)
            {
                return await CheckDomainsInCertificate(configuration, certificate, silent);
            }

            // check uri.
            string applicationUri = Utils.GetApplicationUriFromCertificate(certificate);

            if (String.IsNullOrEmpty(applicationUri))
            {
                bool valid = false;

                string message = "The Application URI could not be read from the certificate. Use certificate anyway?";

                if (!silent && MessageDlg != null)
                {
                    MessageDlg.Message(message, true);
                    if (!await MessageDlg.ShowAsync())
                    {
                        valid = true;
                    }
                }

                Utils.Trace(message);

                if (!valid)
                {
                    return false;
                }
            }

            // update configuration.
            configuration.ApplicationUri = applicationUri;
            configuration.SecurityConfiguration.ApplicationCertificate.Certificate = certificate;

            return true;
        }

        /// <summary>
        /// Checks that the domains in the server addresses match the domains in the certificates.
        /// </summary>
        private static async Task<bool> CheckDomainsInCertificate(
            ApplicationConfiguration configuration,
            X509Certificate2 certificate,
            bool silent)
        {
            Utils.Trace(Utils.TraceMasks.Information, "Checking domains in certificate. {0}", certificate.Subject);

            bool valid = true;
            IList<string> serverDomainNames = configuration.GetServerDomainNames();
            IList<string> certificateDomainNames = Utils.GetDomainsFromCertficate(certificate);

            // get computer name.
            string computerName = Utils.GetHostName();

            // get IP addresses.
            IPAddress[] addresses = await Utils.GetHostAddresses(computerName);

            for (int ii = 0; ii < serverDomainNames.Count; ii++)
            {
                if (Utils.FindStringIgnoreCase(certificateDomainNames, serverDomainNames[ii]))
                {
                    continue;
                }

                if (String.Compare(serverDomainNames[ii], "localhost", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    if (Utils.FindStringIgnoreCase(certificateDomainNames, computerName))
                    {
                        continue;
                    }

                    // check for aliases.
                    bool found = false;

                    // check for ip addresses.
                    for (int jj = 0; jj < addresses.Length; jj++)
                    {
                        if (Utils.FindStringIgnoreCase(certificateDomainNames, addresses[jj].ToString()))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (found)
                    {
                        continue;
                    }
                }

                string message = Utils.Format(
                    "The server is configured to use domain '{0}' which does not appear in the certificate. Use certificate?",
                    serverDomainNames[ii]);

                valid = false;

                if (!silent && MessageDlg != null)
                {
                    MessageDlg.Message(message, true);
                    if (await MessageDlg.ShowAsync())
                    {
                        valid = true;
                        continue;
                    }
                }

                Utils.Trace(message);
                break;
            }

            return valid;
        }
        
        /// <summary>
        /// Adds the application certificate to the discovery server trust list.
        /// </summary>
        public static async Task AddToDiscoveryServerTrustList(
            X509Certificate2 certificate,
            string oldThumbprint,
            IList<X509Certificate2> issuers,
            CertificateStoreIdentifier trustedCertificateStore)
        {
            Utils.Trace(Utils.TraceMasks.Information, "Adding certificate to discovery server trust list.");

            try
            {
                string configurationPath = Utils.GetAbsoluteFilePath(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "OPC Foundation" + Path.DirectorySeparatorChar + "Config" + Path.DirectorySeparatorChar + "Opc.Ua.DiscoveryServer.Config.xml", true, false, false);
                if (configurationPath == null)
                {
                    Utils.Trace("Could not find the discovery server configuration file. Please confirm that it is installed.");
                }
                else
                {
                    Opc.Ua.Security.SecuredApplication ldsConfiguration = new Opc.Ua.Security.SecurityConfigurationManager().ReadConfiguration(configurationPath);
                    CertificateStoreIdentifier csid = Opc.Ua.Security.SecuredApplication.FromCertificateStoreIdentifier(ldsConfiguration.TrustedCertificateStore);
                    await AddApplicationCertificateToStore(csid, certificate, oldThumbprint);

                    if (issuers != null && ldsConfiguration.IssuerCertificateStore != null)
                    {
                        csid = Opc.Ua.Security.SecuredApplication.FromCertificateStoreIdentifier(ldsConfiguration.IssuerCertificateStore);
                        AddIssuerCertificatesToStore(csid, issuers);
                    }

                    CertificateIdentifier cid = Opc.Ua.Security.SecuredApplication.FromCertificateIdentifier(ldsConfiguration.ApplicationCertificate);
                    X509Certificate2 ldsCertificate = await cid.Find(false);

                    // add LDS certificate to application trust list.
                    if (ldsCertificate != null && trustedCertificateStore != null)
                    {
                        await AddApplicationCertificateToStore(csid, ldsCertificate, null);
                    }
                }
            }
            catch (Exception e)
            {
                Utils.Trace(e, "Could not add certificate to discovery server trust list.");
            }
        }

        /// <summary>
        /// Adds an application certificate to a store.
        /// </summary>
        private static async Task AddApplicationCertificateToStore(
            CertificateStoreIdentifier csid,
            X509Certificate2 certificate,
            string oldThumbprint)
        {
            ICertificateStore store = csid.OpenStore();

            try
            {
                // delete the old certificate.
                if (oldThumbprint != null)
                {
                    await store.Delete(oldThumbprint);
                }

                // delete certificates with the same application uri.
                if (store.FindByThumbprint(certificate.Thumbprint) == null)
                {
                    string applicationUri = Utils.GetApplicationUriFromCertificate(certificate);

                    // delete any existing certificates.
                    X509Certificate2Collection collection = await store.Enumerate();
                    foreach (X509Certificate2 target in collection)
                    {
                        if (Utils.CompareDistinguishedName(target.Subject, certificate.Subject))
                        {
                            if (Utils.GetApplicationUriFromCertificate(target) == applicationUri)
                            {
                                await store.Delete(target.Thumbprint);
                            }
                        }
                    }

                    // add new certificate.
                    await store.Add(new X509Certificate2(certificate.RawData));
                }
            }
            finally
            {
                store.Close();
            }
        }

        /// <summary>
        /// Adds an application certificate to a store.
        /// </summary>
        private static void AddIssuerCertificatesToStore(CertificateStoreIdentifier csid, IList<X509Certificate2> issuers)
        {
            ICertificateStore store = csid.OpenStore();

            try
            {
                foreach (X509Certificate2 issuer in issuers)
                {
                    if (store.FindByThumbprint(issuer.Thumbprint) == null)
                    {
                        store.Add(issuer);
                    }
                }
            }
            finally
            {
                store.Close();
            }
        }

        /// <summary>
        /// Adds the certificate to the Trusted Certificate Store
        /// </summary>
        /// <param name="configuration">The application's configuration which specifies the location of the TrustedStore.</param>
        /// <param name="certificate">The certificate to register.</param>
        private static async Task AddToTrustedStore(ApplicationConfiguration configuration, X509Certificate2 certificate)
        {
            if (certificate == null) throw new ArgumentNullException("certificate");

            string storePath = null;

            if (configuration != null && configuration.SecurityConfiguration != null && configuration.SecurityConfiguration.TrustedPeerCertificates != null)
            {
                storePath = configuration.SecurityConfiguration.TrustedPeerCertificates.StorePath;
            }

            if (String.IsNullOrEmpty(storePath))
            {
                Utils.Trace(Utils.TraceMasks.Information, "WARNING: Trusted peer store not specified.");
                return;
            }

            try
            {
                ICertificateStore store = configuration.SecurityConfiguration.TrustedPeerCertificates.OpenStore();

                if (store == null)
                {
                    Utils.Trace("Could not open trusted peer store. StorePath={0}", storePath);
                    return;
                }

                try
                {
                    // check if it already exists.
                    X509Certificate2Collection existingCertificates = await store.FindByThumbprint(certificate.Thumbprint);

                    if (existingCertificates.Count > 0)
                    {
                        return;
                    }

                    Utils.Trace(Utils.TraceMasks.Information, "Adding certificate to trusted peer store. StorePath={0}", storePath);

                    List<string> subjectName = Utils.ParseDistinguishedName(certificate.Subject);

                    // check for old certificate.
                    X509Certificate2Collection certificates = await store.Enumerate();

                    for (int ii = 0; ii < certificates.Count; ii++)
                    {
                        if (Utils.CompareDistinguishedName(certificates[ii], subjectName))
                        {
                            if (certificates[ii].Thumbprint == certificate.Thumbprint)
                            {
                                return;
                            }

                            await store.Delete(certificates[ii].Thumbprint);
                            break;
                        }
                    }

                    // add new certificate.
                    X509Certificate2 publicKey = new X509Certificate2(certificate.RawData);
                    await store.Add(publicKey);
                }
                finally
                {
                    store.Close();
                }
            }
            catch (Exception e)
            {
                Utils.Trace(e, "Could not add certificate to trusted peer store. StorePath={0}", storePath);
            }
        }

        /// <summary>
        /// Gets the access rules to use for the application.
        /// </summary>
        private List<ApplicationAccessRule> GetAccessRules()
        {
            List<ApplicationAccessRule> rules = new List<ApplicationAccessRule>();

            // check for rules specified in the installer configuration.
            bool hasAdmin = false;

            if (InstallConfig.AccessRules != null)
            {
                for (int ii = 0; ii < InstallConfig.AccessRules.Count; ii++)
                {
                    ApplicationAccessRule rule = InstallConfig.AccessRules[ii];

                    if (rule.Right == ApplicationAccessRight.Configure && rule.RuleType == AccessControlType.Allow)
                    {
                        hasAdmin = true;
                        break;
                    }
                }

                rules = InstallConfig.AccessRules;
            }

            // provide some default rules.
            if (rules.Count == 0)
            {
                // give user run access.
                ApplicationAccessRule rule = new ApplicationAccessRule();
                rule.RuleType = AccessControlType.Allow;
                rule.Right = ApplicationAccessRight.Run;
                rule.IdentityName = WellKnownSids.Users;
                rules.Add(rule);

                // ensure service can access.
                if (InstallConfig.InstallAsService)
                {
                    rule = new ApplicationAccessRule();
                    rule.RuleType = AccessControlType.Allow;
                    rule.Right = ApplicationAccessRight.Run;
                    rule.IdentityName = WellKnownSids.NetworkService;
                    rules.Add(rule);

                    rule = new ApplicationAccessRule();
                    rule.RuleType = AccessControlType.Allow;
                    rule.Right = ApplicationAccessRight.Run;
                    rule.IdentityName = WellKnownSids.LocalService;
                    rules.Add(rule);
                }               
            }

            // ensure someone can change the configuration later.
            if (!hasAdmin)
            {
                ApplicationAccessRule rule = new ApplicationAccessRule();
                rule.RuleType = AccessControlType.Allow;
                rule.Right = ApplicationAccessRight.Configure;
                rule.IdentityName = WellKnownSids.Administrators;
                rules.Add(rule);
            }

            return rules;
        }

        /// <summary>
        /// Configures access to the executable, the configuration file and the private key.
        /// </summary>
        private async Task ConfigureFileAccess(ApplicationConfiguration configuration)
        {
            Utils.Trace(Utils.TraceMasks.Information, "Configuring file access.");

            List<ApplicationAccessRule> rules = GetAccessRules();

            // apply access rules to the excutable file.
            try
            {
                if (InstallConfig.SetExecutableFilePermissions)
                {
                    ApplicationAccessRule.SetAccessRules(InstallConfig.ExecutableFile, rules, true);
                }
            }
            catch (Exception e)
            {
                Utils.Trace(e, "Could not set executable file permissions.");
            }

            // apply access rules to the configuration file.
            try
            {
                if (InstallConfig.SetConfigurationFilePermisions)
                {
                    ApplicationAccessRule.SetAccessRules(configuration.SourceFilePath, rules, true);
                }
            }
            catch (Exception e)
            {
                Utils.Trace(e, "Could not set configuration file permissions.");
            }

            // apply access rules to the private key file.
            try
            {
                X509Certificate2 certificate = await configuration.SecurityConfiguration.ApplicationCertificate.Find(true);

                if (certificate != null)
                {
                    ICertificateStore store = configuration.SecurityConfiguration.ApplicationCertificate.OpenStore();
                    store.SetAccessRules(certificate.Thumbprint, rules, true);
                }
            }
            catch (Exception e)
            {
                Utils.Trace(e, "Could not set private key file permissions.");
            }
        }
        #endregion

        #region Private Fields
        private string m_applicationName;
        private ApplicationType m_applicationType;
        private string m_configSectionName;
        private Type m_configurationType;
        private InstalledApplication m_installConfig;
        private ServerBase m_server;
        private ApplicationConfiguration m_applicationConfiguration;
        #endregion
    }
}