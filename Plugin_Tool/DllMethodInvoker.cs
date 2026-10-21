using System;
using System.IO;
using System.Reflection;

public static class DllMethodInvoker
{
    /// <summary>
    /// 加载 DLL 并调用指定类的指定方法。
    /// </summary>
    /// <param name="dllPath">DLL 文件路径</param>
    /// <param name="className">类的完整名称，包含命名空间</param>
    /// <param name="methodName">方法名称</param>
    /// <param name="parameters">方法参数</param>
    /// <returns>方法的返回值；void 方法返回 null</returns>
    public static object Invoke(
        string dllPath,
        string className,
        string methodName,
        object[] parameters = null)
    {
        if (string.IsNullOrWhiteSpace(dllPath))
            throw new ArgumentException("DLL 路径不能为空。");

        if (!File.Exists(dllPath))
            throw new FileNotFoundException("找不到指定的 DLL。", dllPath);

        if (string.IsNullOrWhiteSpace(className))
            throw new ArgumentException("类名不能为空。");

        if (string.IsNullOrWhiteSpace(methodName))
            throw new ArgumentException("方法名不能为空。");

        // 1. 加载 DLL
        Assembly assembly = Assembly.LoadFrom(
            Path.GetFullPath(dllPath));

        // 2. 获取指定类型
        Type type = assembly.GetType(className, false, false);

        if (type == null)
            throw new TypeLoadException(
                "DLL 中找不到指定的类：" + className);

        object[] args = parameters ?? new object[0];

        // 3. 查找参数数量、类型匹配的公开方法
        MethodInfo method = null;

        MethodInfo[] methods = type.GetMethods(
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.Static);

        foreach (MethodInfo item in methods)
        {
            if (item.Name != methodName)
                continue;

            if (item.ContainsGenericParameters)
                continue;

            ParameterInfo[] methodParams = item.GetParameters();

            if (methodParams.Length != args.Length)
                continue;

            bool matched = true;

            for (int i = 0; i < methodParams.Length; i++)
            {
                Type paramType = methodParams[i].ParameterType;

                if (paramType.IsByRef)
                    paramType = paramType.GetElementType();

                if (args[i] == null)
                {
                      if (paramType.IsValueType &&
                        Nullable.GetUnderlyingType(paramType) == null)
                    {
                        matched = false;
                        break;
                    }
                }
                else if (!paramType.IsInstanceOfType(args[i]))
                {
                    // 不自动将 string 转换成 int 等其他类型
                    matched = false;
                    break;
                }
            }

            if (!matched)
                continue;

            if (method != null)
                throw new AmbiguousMatchException(
                    "存在多个匹配的方法，请明确指定参数类型。");

            method = item;
        }

        if (method == null)
            throw new MissingMethodException(
                "找不到匹配的方法：" + className + "." + methodName);

        // 4. 实例方法需要创建对象；静态方法不需要
        object instance = null;

        if (!method.IsStatic)
        {
            instance = Activator.CreateInstance(type);
        }

        // 5. 执行方法并返回结果
        try
        {
            return method.Invoke(instance, args);
        }
        catch (TargetInvocationException ex)
        {
            // 尽量保留插件业务代码真正抛出的异常
            if (ex.InnerException != null)
                throw ex.InnerException;

            throw;
        }
    }
}
