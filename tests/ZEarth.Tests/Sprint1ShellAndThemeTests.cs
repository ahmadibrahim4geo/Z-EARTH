using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;
using ZEarth.App.ViewModels;
using ZEarth.Domain;
using ZEarth.Globe;

namespace ZEarth.Tests
{
    [TestClass]
    public class Sprint1ShellAndThemeTests
    {
        [TestMethod]
        public void SplashViewModel_InitialState_CorrectBranding()
        {
            var vm = new SplashViewModel();
            StringAssert.Contains(vm.VersionString, "0.3.0-preview");
            Assert.IsFalse(vm.IsError);
            Assert.AreEqual(0, vm.ProgressValue);
            Assert.IsNotNull(SplashViewModel.BooleanToVisibilityConverter);
        }

        [TestMethod]
        public async Task SplashViewModel_RunStartupTasks_ProgressUpdatedHonestStages()
        {
            var vm = new SplashViewModel();
            var stages = new List<string>();

            await vm.RunStartupTasksAsync(msg =>
            {
                stages.Add(msg);
                return Task.CompletedTask;
            });

            Assert.AreEqual(5, stages.Count);
            Assert.IsTrue(stages[0].Contains("user settings"));
            Assert.IsTrue(stages[1].Contains("WebView2"));
            Assert.IsTrue(stages[2].Contains("CesiumJS 3D engine"));
            Assert.IsTrue(stages[3].Contains("Places catalog"));
            Assert.IsTrue(stages[4].Contains("Ready"));
            Assert.AreEqual(100, vm.ProgressValue);
        }

        [TestMethod]
        public void SplashViewModel_Cancellation_TriggersCleanAbort()
        {
            var vm = new SplashViewModel();
            Assert.IsFalse(vm.CancellationSource.IsCancellationRequested);

            vm.CancelStartup();
            Assert.IsTrue(vm.CancellationSource.IsCancellationRequested);
            Assert.IsTrue(vm.StatusMessage.Contains("cancelled"));
        }

        [TestMethod]
        public void ThemeService_ThemeSwitching_FiresEventAndUpdatesCurrent()
        {
            var svc = ThemeService.Instance;
            AppTheme? receivedTheme = null;
            svc.ThemeChanged += (s, t) => receivedTheme = t;

            svc.SetTheme(AppTheme.Light);
            Assert.AreEqual(AppTheme.Light, svc.CurrentTheme);
            Assert.AreEqual(AppTheme.Light, receivedTheme);

            svc.SetTheme(AppTheme.Dark);
            Assert.AreEqual(AppTheme.Dark, svc.CurrentTheme);
            Assert.AreEqual(AppTheme.Dark, receivedTheme);
        }

        [TestMethod]
        public void MainViewModel_SidebarDockingAndCollapse_TogglesState()
        {
            var bridge = new GlobeBridge();
            var projSvc = new ProjectService();
            var vm = new MainViewModel(projSvc, bridge);

            // Default state: Docked Left, Not Collapsed
            Assert.IsTrue(vm.IsSidebarDockedLeft);
            Assert.IsFalse(vm.IsSidebarCollapsed);
            Assert.AreEqual("Dock Right", vm.DockToggleLabel);
            Assert.AreEqual("Collapse Sidebar", vm.CollapseToggleLabel);

            // Toggle Dock
            vm.ToggleSidebarDockCommand.Execute(null);
            Assert.IsFalse(vm.IsSidebarDockedLeft);
            Assert.AreEqual("Dock Left", vm.DockToggleLabel);

            // Toggle Collapse
            vm.ToggleSidebarCollapseCommand.Execute(null);
            Assert.IsTrue(vm.IsSidebarCollapsed);
            Assert.AreEqual("Expand Sidebar", vm.CollapseToggleLabel);

            // Restore
            vm.ToggleSidebarDockCommand.Execute(null);
            vm.ToggleSidebarCollapseCommand.Execute(null);
            Assert.IsTrue(vm.IsSidebarDockedLeft);
            Assert.IsFalse(vm.IsSidebarCollapsed);
        }

        [TestMethod]
        public void MainViewModel_StatusEngineInfo_ReflectsSceneModeAndBasemap()
        {
            var bridge = new GlobeBridge();
            var projSvc = new ProjectService();
            var vm = new MainViewModel(projSvc, bridge);

            Assert.IsTrue(vm.StatusEngineInfo.Contains("CesiumJS 1.146.0"));
            Assert.IsTrue(vm.StatusEngineInfo.Contains("3D Globe"));
            Assert.IsTrue(vm.StatusEngineInfo.Contains(vm.ActiveBasemap));

            vm.CurrentSceneMode = SceneMode.Scene2D;
            Assert.IsTrue(vm.StatusEngineInfo.Contains("2D Map"));
        }

        [TestMethod]
        public void MainViewModel_TelemetryCoordinates_FormatProperly()
        {
            var bridge = new GlobeBridge();
            var projSvc = new ProjectService();
            var vm = new MainViewModel(projSvc, bridge);

            vm.CurrentCamera = new CameraState
            {
                Latitude = 30.0444,
                Longitude = 31.2357,
                Altitude = 1500000,
                Heading = 45.0,
                Pitch = -75.0,
                Roll = 0.0
            };

            Assert.IsTrue(vm.StatusCoordinates.Contains("30.0444° N"));
            Assert.IsTrue(vm.StatusCoordinates.Contains("31.2357° E"));
            Assert.IsTrue(vm.StatusAltitude.Contains("km"));
            Assert.AreEqual("45.0°", vm.StatusHeading);
        }
    }
}
