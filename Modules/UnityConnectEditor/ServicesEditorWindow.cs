// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License


using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager.UI;

using Button = UnityEngine.UIElements.Button;

namespace UnityEditor.Connect
{
    internal class ServicesEditorWindow : EditorWindow
    {
        const string k_PrivacyPolicyUrl = "https://unity3d.com/legal/privacy-policy";
        const string k_ServicesWindowUxmlPath = "UXML/ServicesWindow/ServicesWindow.uxml";
        const string k_ServiceTemplateUxmlPath = "UXML/ServicesWindow/ServiceTemplate.uxml";

        const string k_WindowTitle = "Services";

        const string k_ScrollContainerClassName = "scroll-container";
        const string k_ServiceTitleClassName = "service-title";
        const string k_ServiceDescriptionClassName = "service-description";
        const string k_ServiceIconClassName = "service-icon";
        const string k_ServicePackageInstallClassName = "service-package-install";
        const string k_ServicePackageInstallContainerClassName = "service-package-install-container";
        const string k_EntryClassName = "entry";
        const string k_UninstalledEntryClassName = "uninstalled-entry";

        const string k_ServiceStatusClassName = "service-status";
        const string k_ServiceStatusCheckedClassName = "checked";

        const string k_ProjectSettingsBtnName = "ProjectSettingsBtn";
        const string k_PrivacyPolicyLinkName = "PrivacyPolicyLink";
        const string k_DashboardLinkName = "DashboardLink";
        const string k_FooterName = "Footer";

        const string k_ProjectNotBoundMessage = "Project is not bound. Either create a new project Id or bind your project to an existing project Id for the dashboard link to become available.";

        Dictionary<string, Label> m_StatusLabelByServiceName = new Dictionary<string, Label>();
        Dictionary<string, Clickable> m_ClickableByServiceName = new Dictionary<string, Clickable>();
        // These are necessary to list project packages...
        ListRequest m_ListRequestOfPackage;
        SortedList<string, SingleService> m_SortedServices;
        PackageCollection m_PackageCollection;

        static ServicesEditorWindow s_Instance;

        public static ServicesEditorWindow instance => s_Instance;
        public UIElementsNotificationSubscriber notificationSubscriber { get; private set; }

        private struct CloseServiceWindowState
        {
            public int availableServices;
        }

        [MenuItem("Window/General/Services %0", false, 302)]
        internal static void ShowServicesWindow()
        {
            // Opens the window, otherwise focuses it if it’s already open.
            if (s_Instance == null)
            {
                s_Instance = GetWindow<ServicesEditorWindow>(typeof(InspectorWindow));
                s_Instance.titleContent = new GUIContent(L10n.Tr(k_WindowTitle));
                s_Instance.minSize = new Vector2(300, 150);
            }
            else
            {
                GetWindow<ServicesEditorWindow>();
            }
            EditorAnalytics.SendEventShowService(new ServicesProjectSettings.ShowServiceState() { service = k_WindowTitle, page = "", referrer = "window_menu_item"});
        }

        void OnDestroy()
        {
            EditorAnalytics.SendEventCloseServiceWindow(new CloseServiceWindowState() { availableServices = m_StatusLabelByServiceName.Count });
        }

        public void OnEnable()
        {
            if (s_Instance == null)
            {
                s_Instance = this;
            }
            LoadWindow();
        }

        void LoadWindow()
        {
            rootVisualElement.Clear();

            var mainTemplate = EditorGUIUtility.Load(k_ServicesWindowUxmlPath) as VisualTreeAsset;
            rootVisualElement.AddStyleSheetPath(ServicesUtils.StylesheetPath.servicesWindowCommon);
            rootVisualElement.AddStyleSheetPath(EditorGUIUtility.isProSkin ? ServicesUtils.StylesheetPath.servicesWindowDark : ServicesUtils.StylesheetPath.servicesWindowLight);

            mainTemplate.CloneTree(rootVisualElement, new Dictionary<string, VisualElement>(), null);
            notificationSubscriber = new UIElementsNotificationSubscriber(rootVisualElement);
            notificationSubscriber.Subscribe(
                Notification.Topic.AdsService,
                Notification.Topic.AnalyticsService,
                Notification.Topic.BuildService,
                Notification.Topic.CollabService,
                Notification.Topic.CoppaCompliance,
                Notification.Topic.CrashService,
                Notification.Topic.ProjectBind,
                Notification.Topic.PurchasingService
            );

            rootVisualElement.Q<Button>(k_ProjectSettingsBtnName).clicked += () =>
            {
                SettingsService.OpenProjectSettings(GeneralProjectSettings.generalProjectSettingsPath);
            };

            var dashboardClickable = new Clickable(() =>
            {
                if (UnityConnect.instance.projectInfo.projectBound)
                {
                    var dashboardUrl = ServicesConfiguration.instance.GetCurrentProjectDashboardUrl();
                    EditorAnalytics.SendOpenDashboardForService(new ServicesProjectSettings.OpenDashboardForService() { serviceName = k_WindowTitle, url = dashboardUrl });
                    Application.OpenURL(dashboardUrl);
                }
                else
                {
                    NotificationManager.instance.Publish(Notification.Topic.ProjectBind, Notification.Severity.Warning, L10n.Tr(k_ProjectNotBoundMessage));
                    SettingsService.OpenProjectSettings(GeneralProjectSettings.generalProjectSettingsPath);
                }
            });
            rootVisualElement.Q(k_DashboardLinkName).AddManipulator(dashboardClickable);

            m_SortedServices = new SortedList<string, SingleService>();
            bool needProjectListOfPackage = false;
            foreach (var service in ServicesRepository.GetServices())
            {
                m_SortedServices.Add(service.title, service);
                if (service.isPackage && service.packageId != null)
                {
                    needProjectListOfPackage = true;
                }
            }
            // Only launch the listing if a service really needs the packages list...
            m_PackageCollection = null;
            if (needProjectListOfPackage)
            {
                m_ListRequestOfPackage = Client.List();
                EditorApplication.update += ListingCurrentPackageProgress;
            }
            else
            {
                FinalizeServiceSetup();
            }
        }

        void ListingCurrentPackageProgress()
        {
            if (m_ListRequestOfPackage.IsCompleted)
            {
                EditorApplication.update -= ListingCurrentPackageProgress;
                if (m_ListRequestOfPackage.Status == StatusCode.Success)
                {
                    m_PackageCollection = m_ListRequestOfPackage.Result;
                }
                FinalizeServiceSetup();
            }
        }

        void FinalizeServiceSetup()
        {
            var scrollContainer = rootVisualElement.Q(className: k_ScrollContainerClassName);
            var serviceTemplate = EditorGUIUtility.Load(k_ServiceTemplateUxmlPath) as VisualTreeAsset;

            var footer = scrollContainer.Q(k_FooterName);
            scrollContainer.Remove(footer);

            foreach (var singleCloudService in m_SortedServices.Values)
            {
                SetupService(scrollContainer, serviceTemplate, singleCloudService);
            }

            scrollContainer.Add(footer);

            var privacyClickable = new Clickable(() =>
            {
                Application.OpenURL(k_PrivacyPolicyUrl);
            });
            scrollContainer.Q(k_PrivacyPolicyLinkName).AddManipulator(privacyClickable);
        }

        void SetupService(VisualElement scrollContainer, VisualTreeAsset serviceTemplate, SingleService singleService)
        {
            scrollContainer.Add(serviceTemplate.CloneTree().contentContainer);
            var serviceIconAsset = EditorGUIUtility.Load(singleService.pathTowardIcon) as Texture2D;
            var serviceRoot = scrollContainer[scrollContainer.childCount - 1];
            serviceRoot.name = singleService.name;

            var serviceTitle = serviceRoot.Q<TextElement>(className: k_ServiceTitleClassName);
            serviceTitle.text = singleService.title;
            serviceRoot.Q<TextElement>(className: k_ServiceDescriptionClassName).text = singleService.description;
            serviceRoot.Q(className: k_ServiceIconClassName).style.backgroundImage = serviceIconAsset;

            Action openServiceSettingsLambda = () =>
            {
                SettingsService.OpenProjectSettings(singleService.projectSettingsPath);
            };
            m_ClickableByServiceName.Add(singleService.name, new Clickable(openServiceSettingsLambda));

            SetupServiceStatusLabel(serviceRoot, singleService);
            SetupPackageInstall(serviceRoot, singleService);
        }

        void SetupPackageInstall(VisualElement serviceRoot, SingleService singleService)
        {
            serviceRoot.AddManipulator(m_ClickableByServiceName[singleService.name]);

            var installButton = serviceRoot.Q<Button>(className: k_ServicePackageInstallClassName);
            var installButtonContainer = serviceRoot.Q(className: k_ServicePackageInstallContainerClassName);
            installButtonContainer.style.display = DisplayStyle.None;

            if (singleService.isPackage && (singleService.packageId != null) && (m_PackageCollection != null))
            {
                SetServiceToUninstalledState(installButton, serviceRoot, singleService);
                bool packageFound = false;
                foreach (var info in m_PackageCollection)
                {
                    if (info.packageId.Contains(singleService.packageId))
                    {
                        packageFound = true;
                        SetServiceToInstalledState(installButton, serviceRoot, singleService);
                        break;
                    }
                }

                if (!packageFound)
                {
                    SetServiceToUninstalledState(installButton, serviceRoot, singleService);

                    installButtonContainer.style.display = DisplayStyle.Flex;
                    installButton.clicked += () =>
                    {
                        var packageId = singleService.packageId;
                        EditorAnalytics.SendOpenPackManFromServiceSettings(new ServicesProjectSettings.OpenPackageManager() { packageName = packageId });
                        PackageManagerWindow.OpenPackageManager(packageId);
                    };
                }
            }
        }

        void SetServiceToUninstalledState(Button installButton, VisualElement serviceRoot, SingleService singleService)
        {
            installButton.style.display = DisplayStyle.Flex;
            serviceRoot.RemoveManipulator(m_ClickableByServiceName[singleService.name]);
            serviceRoot[0].RemoveFromClassList(k_EntryClassName);
            serviceRoot[0].AddToClassList(k_UninstalledEntryClassName);
            if (singleService.displayToggle)
            {
                m_StatusLabelByServiceName[singleService.name].style.display = DisplayStyle.None;
            }
        }

        void SetServiceToInstalledState(Button installButton, VisualElement serviceRoot, SingleService singleService)
        {
            installButton.style.display = DisplayStyle.None;
            serviceRoot.AddManipulator(m_ClickableByServiceName[singleService.name]);
            serviceRoot[0].RemoveFromClassList(k_UninstalledEntryClassName);
            serviceRoot[0].AddToClassList(k_EntryClassName);
            if (singleService.displayToggle)
            {
                SetServiceStatusValue(singleService.name, singleService.IsServiceEnabled());
                m_StatusLabelByServiceName[singleService.name].style.display = DisplayStyle.Flex;
            }
        }

        void SetupServiceStatusLabel(VisualElement serviceRoot, SingleService singleService)
        {
            var statusText = serviceRoot.Q<Label>(className: k_ServiceStatusClassName);
            m_StatusLabelByServiceName.Add(singleService.name, statusText);
            SetServiceStatusValue(singleService.name, singleService.IsServiceEnabled());

            if (singleService.displayToggle)
            {
                statusText.style.display = DisplayStyle.Flex;
            }
            else
            {
                statusText.style.display = DisplayStyle.None;
            }
        }

        public void SetServiceStatusValue(string serviceName, bool active)
        {
            if (m_StatusLabelByServiceName.ContainsKey(serviceName))
            {
                m_StatusLabelByServiceName[serviceName].text = active ? L10n.Tr("ON") : L10n.Tr("OFF");
                if (active)
                {
                    m_StatusLabelByServiceName[serviceName].AddToClassList(k_ServiceStatusCheckedClassName);
                }
                else
                {
                    m_StatusLabelByServiceName[serviceName].RemoveFromClassList(k_ServiceStatusCheckedClassName);
                }
            }
        }
    }
}
