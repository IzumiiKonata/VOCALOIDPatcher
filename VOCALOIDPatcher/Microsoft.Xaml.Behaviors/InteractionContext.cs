using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Resources;

namespace Microsoft.Xaml.Behaviors;

internal static class InteractionContext
{
	private static Assembly runtimeAssembly;

	private static object playerContextInstance;

	private static object activeNavigationViewModelObject;

	private static PropertyInfo libraryNamePropertyInfo;

	private static PropertyInfo activeNavigationViewModelPropertyInfo;

	private static PropertyInfo canGoBackPropertyInfo;

	private static PropertyInfo canGoForwardPropertyInfo;

	private static PropertyInfo sketchFlowAnimationPlayerPropertyInfo;

	private static MethodInfo goBackMethodInfo;

	private static MethodInfo goForwardMethodInfo;

	private static MethodInfo navigateToScreenMethodInfo;

	private static MethodInfo invokeStateChangeMethodInfo;

	private static MethodInfo playSketchFlowAnimationMethodInfo;

	private static NavigationService navigationService;

	private static readonly string LibraryName;

	private static readonly Dictionary<string, Serializer.Data> NavigationData;

	public static object ActiveNavigationViewModelObject
	{
		get
		{
			return activeNavigationViewModelObject ?? activeNavigationViewModelPropertyInfo.GetValue(playerContextInstance, null);
		}
		internal set
		{
			activeNavigationViewModelObject = value;
		}
	}

	private static bool IsPrototypingRuntimeLoaded => runtimeAssembly != null;

	private static bool CanGoBack => (bool)canGoBackPropertyInfo.GetValue(ActiveNavigationViewModelObject, null);

	private static bool CanGoForward => (bool)canGoForwardPropertyInfo.GetValue(ActiveNavigationViewModelObject, null);

	private static bool PlatformCanGoBack
	{
		get
		{
			if (navigationService != null)
			{
				return navigationService.CanGoBack;
			}
			return false;
		}
	}

	private static bool PlatformCanGoForward
	{
		get
		{
			if (navigationService != null)
			{
				return navigationService.CanGoForward;
			}
			return false;
		}
	}

	static InteractionContext()
	{
		NavigationData = new Dictionary<string, Serializer.Data>(StringComparer.OrdinalIgnoreCase);
		runtimeAssembly = FindPlatformRuntimeAssembly();
		if (runtimeAssembly != null)
		{
			InitializeRuntimeNavigation();
			LibraryName = (string)libraryNamePropertyInfo.GetValue(playerContextInstance, null);
			LoadNavigationData(LibraryName);
		}
		else
		{
			InitalizePlatformNavigation();
		}
	}

	public static void GoBack()
	{
		if (IsPrototypingRuntimeLoaded)
		{
			if (CanGoBack)
			{
				goBackMethodInfo.Invoke(ActiveNavigationViewModelObject, null);
			}
		}
		else
		{
			PlatformGoBack();
		}
	}

	public static void GoForward()
	{
		if (IsPrototypingRuntimeLoaded)
		{
			if (CanGoForward)
			{
				goForwardMethodInfo.Invoke(ActiveNavigationViewModelObject, null);
			}
		}
		else
		{
			PlatformGoForward();
		}
	}

	public static bool IsScreen(string screenName)
	{
		if (!IsPrototypingRuntimeLoaded)
		{
			return false;
		}
		return GetScreenClassName(screenName) != null;
	}

	public static void GoToScreen(string screenName, Assembly assembly)
	{
		if (IsPrototypingRuntimeLoaded)
		{
			string screenClassName = GetScreenClassName(screenName);
			if (!string.IsNullOrEmpty(screenClassName))
			{
				object[] parameters = new object[2] { screenClassName, true };
				navigateToScreenMethodInfo.Invoke(ActiveNavigationViewModelObject, parameters);
			}
		}
		else if (!(assembly == null))
		{
			AssemblyName assemblyName = new AssemblyName(assembly.FullName);
			if (assemblyName != null)
			{
				PlatformGoToScreen(assemblyName.Name, screenName);
			}
		}
	}

	public static void GoToState(string screen, string state)
	{
		if (!string.IsNullOrEmpty(screen) && !string.IsNullOrEmpty(state) && IsPrototypingRuntimeLoaded)
		{
			object[] parameters = new object[3] { screen, state, false };
			invokeStateChangeMethodInfo.Invoke(ActiveNavigationViewModelObject, parameters);
		}
	}

	public static void PlaySketchFlowAnimation(string sketchFlowAnimation, string owningScreen)
	{
		if (!string.IsNullOrEmpty(sketchFlowAnimation) && !string.IsNullOrEmpty(owningScreen) && IsPrototypingRuntimeLoaded)
		{
			object value = activeNavigationViewModelPropertyInfo.GetValue(playerContextInstance, null);
			object[] parameters = new object[2] { sketchFlowAnimation, owningScreen };
			playSketchFlowAnimationMethodInfo.Invoke(value, parameters);
		}
	}

	private static void InitializeRuntimeNavigation()
	{
		Type? type = runtimeAssembly.GetType("Microsoft.Expression.Prototyping.Services.PlayerContext");
		PropertyInfo property = type.GetProperty("Instance");
		activeNavigationViewModelPropertyInfo = type.GetProperty("ActiveNavigationViewModel");
		libraryNamePropertyInfo = type.GetProperty("LibraryName");
		playerContextInstance = property.GetValue(null, null);
		Type? type2 = runtimeAssembly.GetType("Microsoft.Expression.Prototyping.Navigation.NavigationViewModel");
		canGoBackPropertyInfo = type2.GetProperty("CanGoBack");
		canGoForwardPropertyInfo = type2.GetProperty("CanGoForward");
		goBackMethodInfo = type2.GetMethod("GoBack");
		goForwardMethodInfo = type2.GetMethod("GoForward");
		navigateToScreenMethodInfo = type2.GetMethod("NavigateToScreen");
		invokeStateChangeMethodInfo = type2.GetMethod("InvokeStateChange");
		playSketchFlowAnimationMethodInfo = type2.GetMethod("PlaySketchFlowAnimation");
		sketchFlowAnimationPlayerPropertyInfo = type2.GetProperty("SketchFlowAnimationPlayer");
	}

	private static Serializer.Data LoadNavigationData(string assemblyName)
	{
		Serializer.Data value = null;
		if (NavigationData.TryGetValue(assemblyName, out value))
		{
			return value;
		}
		_ = Application.Current;
		string uriString = string.Format(CultureInfo.InvariantCulture, "/{0};component/Sketch.Flow", assemblyName);
		try
		{
			StreamResourceInfo resourceStream = Application.GetResourceStream(new Uri(uriString, UriKind.Relative));
			if (resourceStream != null)
			{
				value = Serializer.Deserialize(resourceStream.Stream);
				NavigationData[assemblyName] = value;
			}
		}
		catch (IOException)
		{
		}
		catch (InvalidOperationException)
		{
		}
		return value ?? new Serializer.Data();
	}

	private static string GetScreenClassName(string screenName)
	{
		Serializer.Data value = null;
		NavigationData.TryGetValue(LibraryName, out value);
		if (value == null || value.Screens == null)
		{
			return null;
		}
		if (!value.Screens.Any((Serializer.Data.Screen screen) => screen.ClassName == screenName))
		{
			screenName = (from screen in value.Screens
				where screen.DisplayName == screenName
				select screen.ClassName).FirstOrDefault();
		}
		return screenName;
	}

	private static void InitalizePlatformNavigation()
	{
		if (Application.Current.MainWindow is NavigationWindow navigationWindow)
		{
			navigationService = navigationWindow.NavigationService;
		}
	}

	private static Assembly FindPlatformRuntimeAssembly()
	{
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (Assembly assembly in assemblies)
		{
			if (assembly.GetName().Name.Equals("Microsoft.Expression.Prototyping.Runtime"))
			{
				return assembly;
			}
		}
		return null;
	}

	public static void PlatformGoBack()
	{
		if (navigationService != null && PlatformCanGoBack)
		{
			navigationService.GoBack();
		}
	}

	public static void PlatformGoForward()
	{
		if (navigationService != null && PlatformCanGoForward)
		{
			navigationService.GoForward();
		}
	}

	public static void PlatformGoToScreen(string assemblyName, string screen)
	{
		ObjectHandle objectHandle = Activator.CreateInstance(assemblyName, screen);
		navigationService.Navigate(objectHandle.Unwrap());
	}
}
