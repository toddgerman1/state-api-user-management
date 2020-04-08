using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Fathym;
using LCU.Presentation.State.ReqRes;
using LCU.StateAPI.Utilities;
using LCU.StateAPI;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;
using System.Collections.Generic;
using LCU.Personas.Client.Enterprises;
using LCU.Personas.Client.DevOps;
using LCU.Personas.Enterprises;
using LCU.Personas.Client.Applications;
using LCU.Personas.Client.Identity;
using Fathym.API;

namespace LCU.State.API.NapkinIDE.UserManagement
{
    public class UserManagementStateHarness : LCUStateHarness<UserManagementState>
    {
        #region Fields 
        #endregion

        #region Properties 
        #endregion

        #region Constructors
        public UserManagementStateHarness(UserManagementState state)
            : base(state ?? new UserManagementState())
        { }
        #endregion

        #region API Methods
        public virtual async Task<Status> BootOrganizationEnvironment(EnterpriseArchitectClient entArch, EnterpriseManagerClient entMgr,
            DevOpsArchitectClient devOpsArch, string parentEntApiKey, string username)
        {
            var status = Status.Success;

            if (State.NewEnterpriseAPIKey.IsNullOrEmpty())
            {
                var entRes = await entArch.CreateEnterprise(new CreateEnterpriseRequest()
                {
                    Description = State.OrganizationDescription ?? State.OrganizationName,
                    Host = State.Host,
                    Name = State.OrganizationName
                }, parentEntApiKey, username);

                State.NewEnterpriseAPIKey = entRes.Model?.PrimaryAPIKey;

                status = entRes.Status;
            }

            if (status && !State.NewEnterpriseAPIKey.IsNullOrEmpty() && State.EnvironmentLookup.IsNullOrEmpty())
            {
                var envResp = await devOpsArch.EnsureEnvironment(new Personas.DevOps.EnsureEnvironmentRequest()
                {
                    EnvSettings = State.EnvSettings,
                    OrganizationLookup = State.OrganizationLookup,
                }, State.NewEnterpriseAPIKey);

                State.EnvironmentLookup = envResp.Model?.Lookup;

                status = envResp.Status;
            }
            else if (!State.NewEnterpriseAPIKey.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
                await entMgr.SaveEnvironmentSettings(State.EnvSettings, State.NewEnterpriseAPIKey, State.EnvironmentLookup);

            UpdateStatus(status);

            return status;
        }

        public virtual async Task<Status> BootIaC(DevOpsArchitectClient devOpsArch, string parentEntApiKey, string username)
        {
            if (!State.NewEnterpriseAPIKey.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var resp = await devOpsArch.EnsureInfrastructureRepo(State.NewEnterpriseAPIKey, username, State.EnvironmentLookup, devOpsEntApiKey: parentEntApiKey);

                return resp.Status;
            }
            else
                return Status.Success;
        }

        public virtual async Task<Status> BootIaCBuildsAndReleases(DevOpsArchitectClient devOpsArch, string parentEntApiKey, string username)
        {
            if (!State.NewEnterpriseAPIKey.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var resp = await devOpsArch.EnsureInfrastructureBuildAndRelease(State.NewEnterpriseAPIKey, username, State.EnvironmentLookup, devOpsEntApiKey: parentEntApiKey);

                return resp.Status;
            }
            else
                return Status.Success;
        }

        public virtual async Task<Status> BootDAFInfrastructure(DevOpsArchitectClient devOpsArch, string parentEntApiKey, string username)
        {
            if (!State.NewEnterpriseAPIKey.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var resp = await devOpsArch.SetEnvironmentInfrastructure(new Personas.DevOps.SetEnvironmentInfrastructureRequest()
                {
                    Template = State.Template
                }, State.NewEnterpriseAPIKey, State.EnvironmentLookup, username, devOpsEntApiKey: parentEntApiKey);

                return resp.Status;
            }
            else
                return Status.Success;
        }

        public virtual async Task<Status> BootHost(EnterpriseArchitectClient entArch, string parentEntApiKey)
        {
            if (!State.NewEnterpriseAPIKey.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                // var response = await entArch.EnsureHost(new EnsureHostRequest()
                // {
                //     EnviromentLookup = State.EnvironmentLookup
                // }, State.NewEnterpriseAPIKey, State.Host, State.EnvironmentLookup, parentEntApiKey);
                var response = await entArch.Post<EnsureHostRequest, BaseResponse>($"hosting/{State.NewEnterpriseAPIKey}/hosts/{State.Host}/ensure?envLookup={State.EnvironmentLookup}&parentEntApiKey={parentEntApiKey}",
                    new EnsureHostRequest()
                    {
                        EnviromentLookup = State.EnvironmentLookup
                    });

                return response.Status;
            }
            else
                return Status.Success;
        }

        public virtual async Task<Status> BootHostAuthApp(EnterpriseArchitectClient entArch)
        {
            if (!State.NewEnterpriseAPIKey.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var resp = await entArch.EnsureHostAuthApp(State.NewEnterpriseAPIKey, State.Host, State.EnvironmentLookup);

                return resp.Status;
            }
            else
                return Status.Success;
        }

        public virtual async Task<Status> BootHostSSL(EnterpriseArchitectClient entArch, string parentEntApiKey)
        {
            if (!State.NewEnterpriseAPIKey.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var resp = await entArch.EnsureHostsSSL(new EnsureHostsSSLRequest()
                {
                    Hosts = new List<string>() { State.Host }
                }, State.NewEnterpriseAPIKey, State.EnvironmentLookup, parentEntApiKey);

                return resp.Status;
            }
            else
                return Status.Success;
        }

        public virtual async Task<Status> BootMicroAppsRuntime(EnterpriseArchitectClient entArch)
        {
            if (!State.NewEnterpriseAPIKey.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var resp = await entArch.EnsureLCURuntime(State.NewEnterpriseAPIKey, State.EnvironmentLookup);

                return resp.Status;
            }
            else
                return Status.Success;
        }

        public virtual async Task<Status> BootDataApps(ApplicationDeveloperClient appDev)
        {
            if (!State.NewEnterpriseAPIKey.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var resp = await appDev.ConfigureNapkinIDEForDataApps(State.NewEnterpriseAPIKey, State.Host);

                return resp.Status;
            }
            else
                return Status.Success;
        }

        public virtual async Task<Status> BootDataFlow(ApplicationDeveloperClient appDev)
        {
            if (!State.NewEnterpriseAPIKey.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var resp = await appDev.ConfigureNapkinIDEForDataFlows(State.NewEnterpriseAPIKey, State.Host);

                return resp.Status;
            }
            else
                return Status.Success;
        }

        public virtual async Task<Status> BootLCUFeeds(DevOpsArchitectClient devOpsArch, string parentEntApiKey, string username)
        {
            if (!State.NewEnterpriseAPIKey.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var resp = await devOpsArch.EnsureLCUFeed(new Personas.DevOps.EnsureLCUFeedRequest()
                {
                    EnvironmentLookup = State.EnvironmentLookup
                }, State.NewEnterpriseAPIKey, username, devOpsEntApiKey: parentEntApiKey);

                return resp.Status;
            }
            else
                return Status.Success;
        }

        public virtual async Task<Status> BootTaskLibrary(DevOpsArchitectClient devOpsArch, string parentEntApiKey, string username)
        {
            if (!State.NewEnterpriseAPIKey.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var resp = await devOpsArch.EnsureTaskTlibrary(State.NewEnterpriseAPIKey, username, State.EnvironmentLookup, devOpsEntApiKey: parentEntApiKey);

                return resp.Status;
            }
            else
                return Status.Success;
        }

        public virtual async Task<Status> CanFinalize(EnterpriseManagerClient entMgr, string parentEntApiKey, string username)
        {
            var status = Status.GeneralError;

            if (!State.NewEnterpriseAPIKey.IsNullOrEmpty() && !State.EnvironmentLookup.IsNullOrEmpty())
            {
                var canFinalize = await entMgr.EnsureInfraBuiltAndReleased(State.NewEnterpriseAPIKey, username, State.EnvironmentLookup, parentEntApiKey);

                status = canFinalize.Status;
            }

            return status;
        }

        public virtual void CompleteBoot()
        {
            State.Booted = true;
        }


        public virtual void ConfigureInfrastructure(string infraType, bool useDefaultSettings, MetadataModel settings, string template)
        {
            var envLookup = $"{State.OrganizationLookup}-prd";

            State.Booted = false;

            State.EnvSettings = settings;

            State.Template = template;

            SetNapkinIDESetupStep(NapkinIDESetupStepTypes.Review);
        }

        public virtual void ConfigureBootOptions()
        {
            State.BootOptions = new List<BootOption>();

            State.BootOptions.Add(new BootOption()
            {
                Name = "Project Details Configured",
                Lookup = "Project",
                Description = "Used for data configuration, project setup, and default secure-hosting",
                SetupStep = NapkinIDESetupStepTypes.OrgDetails
            });

            State.BootOptions.Add(new BootOption()
            {
                Name = "Connected with DevOps",
                Lookup = "DevOps",
                Description = "Source Control, Builds, Deployment"
            });

            State.BootOptions.Add(new BootOption()
            {
                Name = "Infrastructure Connected",
                Lookup = "Infrastructure",
                Description = "A scalable, cost effective infrastructure configuration",
                SetupStep = NapkinIDESetupStepTypes.AzureSetup
            });

            State.BootOptions.Add(new BootOption()
            {
                Name = "Domain Configuration",
                Lookup = "Domain",
                Description = "User Security, Host Setup, Free Open Source SSL"
            });

            State.BootOptions.Add(new BootOption()
            {
                Name = "Micro-Application Orchestration",
                Lookup = "MicroApps",
                Description = "Low-Code Unit™ Runtime, Data Flow Low-Code Unit™, Data Applications Low-Code Unit™"
            });
        }

        public virtual void ConfigureInfrastructureOptions()
        {
            State.InfrastructureOptions = new Dictionary<string, string>();

            State.InfrastructureOptions["fathym\\daf-state-setup"] = "Low-Code Unit™ Runtime";

            State.InfrastructureOptions["fathym\\daf-iot-full-setup"] = "Low-Code Unit™ Runtime w/ IoT";
        }

        public virtual void ConfigurePersonas()
        {
            // if (state.Personas.IsNullOrEmpty())
            State.Personas = new List<JourneyPersona>()
            {
                new JourneyPersona()
                {
                    Name = "Developer Journeys",
                    Lookup = "Develop",
                    Descriptions = new List<string>() {
                        "Start from a number of developer journeys that will get you up and running in minutes."
                    },
                    DetailLookupCategories = new Dictionary<string, List<string>>()
                    {
                        {
                            "Featured", new List<string>()
                            {
                                "AngularSPA",
                                "LCUBlade",
                                "EdgeToApp",
                                "PowerBIDataApps"
                            }
                        }
                    }
                },
                // new JourneyPersona()
                // {
                //     Name = "Designer Journeys",
                //     Lookup = "Design",
                //     Descriptions = new List<string>() {
                //         "Start from a number of designer journeys that will get you up and running in minutes."
                //     }
                // },
                new JourneyPersona()
                {
                    Name = "Admin Journeys",
                    Lookup = "Manage",
                    Descriptions = new List<string>() {
                        "Start from a number of admin journeys that will get you up and running in minutes."
                    },
                    DetailLookupCategories = new Dictionary<string, List<string>>()
                    {
                        {
                            "Featured", new List<string>()
                            {
                                "UserSetup",
                                "PowerBIDataApps",
                                "ContainerDeployment"
                            }
                        }
                    }
                }
            };
        }

        public virtual void ConfigureJourneys()
        {
            State.Details = new List<JourneyDetail>()
            {
                new JourneyDetail()
                {
                    Name = "SPAs with Angular",
                    Lookup = "AngularSPA",
                    Description = "Create and host your next Angular application with Fathym's Low-Code Unit™."
                },
                new JourneyDetail()
                {
                    Name = "Low-Code Unit™ Blade",
                    Lookup = "LCUBlade",
                    Description = "Create a new Low-Code Unit™ Blade for your Enterprise IDE."
                },
                new JourneyDetail()
                {
                    Name = "Edge to App",
                    Lookup = "EdgeToApp",
                    Description = "Leverage a number of edge devices to explore the workflow for delivering edge data to customer applications."
                },
                new JourneyDetail()
                {
                    Name = "Power BI Data Applications",
                    Lookup = "PowerBIDataApps",
                    Description = "Securely host and deliver your PowerBI reports internally and with customers."
                },
                new JourneyDetail()
                {
                    Name = "Build a Dashboard",
                    Lookup = "DashboardBasic",
                    Description = "Build a dashboard rapidly."
                },
                new JourneyDetail()
                {
                    Name = "Deploy Freeboard",
                    Lookup = "DashboardFreeboard",
                    Description = "Build a freeobard deployment rapidly."
                },
                new JourneyDetail()
                {
                    Name = "User Setup",
                    Lookup = "UserSetup",
                    Description = "Complete your user profile."
                },
                new JourneyDetail()
                {
                    Name = "Container Deployment Strategy",
                    Lookup = "ContainerDeployment",
                    Description = "Setup and configure your enterprise container deployment strategy."
                },
                new JourneyDetail()
                {
                    Name = "Splunk for Enterprise",
                    Lookup = "SplunkEnterprise",
                    Description = "Splunk enterprise setup in a snap."
                },
                new JourneyDetail()
                {
                    Name = "Open Source your Legacy",
                    Lookup = "OpenSourceLegacy",
                    Description = "A pathway to moving your enterprise legacy applications to the open source."
                },
                new JourneyDetail()
                {
                    Name = "Onboard ABB Flow Device",
                    Lookup = "ABB G5 Flow Device",
                    Description = "A pathway to moving your enterprise legacy applications to the open source."
                },
                new JourneyDetail()
                {
                    Name = "Fathym Classic for Enterprise",
                    Lookup = "FathymClassicEnterprise",
                    Description = "Fathym Classic enterprise setup in a snap."
                }
            };
        }

        public virtual void DetermineSetupStep()
        {
            if (State.OrganizationName.IsNullOrEmpty())
                State.SetupStep = NapkinIDESetupStepTypes.OrgDetails;
        }

        public virtual async Task HasDevOpsOAuth(EnterpriseManagerClient entMgr, string entApiKey, string username)
        {
            var hasDevOps = await entMgr.HasDevOpsOAuth(entApiKey, username);

            State.HasDevOpsOAuth = hasDevOps.Status;
        }


        public virtual async Task ListSubscribers(IdentityManagerClient idMgr, string entApiKey, string isLimited)
        {
            // Get the list of subscribers based on subscriber status
            var subscriberResp = await idMgr.ListSubscribers(entApiKey, isLimited);

            // Update subscriber state
            if (isLimited == "true")
            {
                State.SubscribersLimited = subscriberResp.Model;
            }
            else
            {
                State.SubscribersActive = subscriberResp.Model;
            }

        }

        public virtual async Task LoadRegistrationHosts(EnterpriseManagerClient entMgr, string entApiKey)
        {
            if (State.HostOptions.IsNullOrEmpty())
            {
                var regHosts = await entMgr.ListRegistrationHosts(entApiKey);

                State.HostOptions = regHosts.Model;
            }
        }

        public virtual void SecureHost()
        {
            var root = State.HostOptions.FirstOrDefault();

            State.Host = $"{State.OrganizationLookup}.{root}";
        }

        public virtual void SetBootOptionsLoading()
        {
            State.BootOptions.ForEach(bo =>
            {
                bo.Loading = true;
            });
        }

        public virtual void UpdateStatus(Status status)
        {
            State.Status = status;
        }

        public virtual void UpdateBootOption(string bootOptionLookup, Status status = null, bool? loading = null)
        {
            var bootOption = State.BootOptions.FirstOrDefault(bo => bo.Lookup == bootOptionLookup);

            if (bootOption != null)
            {
                if (status != null)
                    bootOption.Status = status;

                if (loading.HasValue)
                    bootOption.Loading = loading.Value;
            }
        }

        public virtual void SetNapkinIDESetupStep(NapkinIDESetupStepTypes step)
        {
            State.SetupStep = step;

            if (State.SetupStep == NapkinIDESetupStepTypes.Review)
                ConfigureBootOptions();
        }

        public virtual async Task SetOrganizationDetails(EnterpriseManagerClient entMgr, string name, string description, string lookup, bool accepted)
        {
            var hostResp = await entMgr.ResolveHost(State.Host, false);

            if (hostResp.Status == Status.NotLocated)
            {
                State.OrganizationName = name;

                State.OrganizationDescription = description;

                State.OrganizationLookup = lookup;

                State.TermsAccepted = accepted;

                SecureHost();

                if (!name.IsNullOrEmpty())
                    SetNapkinIDESetupStep(NapkinIDESetupStepTypes.AzureSetup);
                else
                    SetNapkinIDESetupStep(NapkinIDESetupStepTypes.OrgDetails);
            }
            else
                State.Status = new Status()
                {
                    Code = 101,
                    Message = "An enterprise with that lookup already exists."
                };
        }

        public virtual void SetUserType(UserTypes userType)
        {
            State.UserType = userType;
        }
        #endregion
    }
}
