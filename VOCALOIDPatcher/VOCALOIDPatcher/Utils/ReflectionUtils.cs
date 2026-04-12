using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using HarmonyLib;
using Yamaha.VOCALOID;

namespace VOCALOIDPatcher.Utils;

public static class ReflectionUtils
{
    public static MainWindow GetMainWindow()
    {
        if (Application.Current?.MainWindow is MainWindow mainWindow)
        {
            return mainWindow;
        }

        throw new InvalidOperationException("获取 MainWindow 失败。");
    }

    public static Menu GetMainMenu()
    {
        var mainWindow = GetMainWindow();

        var field = AccessTools.Field(mainWindow.GetType(), "xMainMenu")
                    ?? throw new MissingFieldException(mainWindow.GetType().FullName, "xMainMenu");

        return field.GetValue(mainWindow) as Menu
               ?? throw new InvalidCastException("获取 xMainMenu 失败。");
    }

    public static T GetMainWindowField<T>(string fieldName) where T: class
    {
        var mainWindow = GetMainWindow();
        var mainWindowType = mainWindow.GetType();
        var fieldInfo = mainWindowType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        if (fieldInfo == null)
            throw new MissingFieldException(mainWindow.GetType().FullName + "." + fieldName, fieldName);
        
        return fieldInfo.GetValue(mainWindow) as T ?? throw new InvalidCastException(mainWindow.GetType().FullName + "." + fieldName);
    }

    public static TFieldType GetField<TFieldType>(object holderInstance, string fieldName) 
        where TFieldType : class
    {
        var type = holderInstance.GetType();
        FieldInfo? fieldInfo  = AccessTools.Field(type, fieldName);

        if (fieldInfo == null)
            throw new MissingFieldException(type.FullName + "." + fieldName, fieldName);
        
        return fieldInfo.GetValue(holderInstance) as TFieldType ?? throw new InvalidCastException(type.FullName + "." + fieldName);
    }
    
    public static object GetField(object holderInstance, string fieldName) 
    {
        var type = holderInstance.GetType();
        FieldInfo? fieldInfo = AccessTools.Field(type, fieldName);

        if (fieldInfo == null)
            throw new MissingFieldException(type.FullName + "." + fieldName, fieldName);
        
        return fieldInfo.GetValue(holderInstance) ?? throw new InvalidCastException(type.FullName + "." + fieldName);
    }

    public static TFieldType GetFirstFieldWithType<TFieldType>(object holderInstance) 
        where TFieldType : class
    {
        var type = holderInstance.GetType();
        var declaredFields = AccessTools.GetDeclaredFields(type);

        var fieldInfo = declaredFields.Find(field => field.FieldType == typeof(TFieldType));
        if (fieldInfo == null)
            throw new MissingFieldException(type.FullName + ", typeof " + typeof(TFieldType).FullName, typeof(TFieldType).FullName);

        return (TFieldType) (fieldInfo.GetValue(holderInstance) ?? throw new MissingFieldException(type.FullName + ", typeof " + typeof(TFieldType).FullName,typeof(TFieldType).FullName));
    }
}