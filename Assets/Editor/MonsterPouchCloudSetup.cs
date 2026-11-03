using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

// Invokes the editor's own project workflow using its existing signed-in session.
// Never reads, exports, stores or logs access tokens.
public static class MonsterPouchCloudSetup
{
    const BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static bool busy;
    static Type Find(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).FirstOrDefault(t=>t!=null)
        ??throw new InvalidOperationException("Editor API unavailable: "+name);
    static object Value(object obj,string name)=>obj.GetType().GetProperty(name,Any)?.GetValue(obj)??obj.GetType().GetField(name,Any)?.GetValue(obj);
    static string Field(object obj,string name)=>Value(obj,name)?.ToString()??"";
    static object Connection=>Find("UnityEditor.Connect.UnityConnect").GetProperty("instance",Any).GetValue(null);
    static async Task<object> Request(string method,params object[] supplied)
    {
        var api=Find("UnityEditor.Connect.UnityConnectRequests");
        var call=api.GetMethods(Any).Single(m=>m.Name==method);
        var parameters=call.GetParameters();var args=new object[parameters.Length];
        for(int i=0;i<args.Length;i++)args[i]=i<supplied.Length?supplied[i]:parameters[i].ParameterType==typeof(CancellationToken)?CancellationToken.None:parameters[i].DefaultValue;
        var task=(Task)call.Invoke(null,args);await task;
        return task.GetType().GetProperty("Result").GetValue(task);
    }
    public static string Discover()
    {
        if(busy)return "Cloud request already in progress.";
        if(!(bool)Value(Connection,"loggedIn"))return "Unity session is signed out. Sign in through Unity Hub.";
        DiscoverAsync();return "Cloud discovery queued. See cloud-discovery.txt.";
    }
    static async void DiscoverAsync()
    {
        busy=true;
        try
        {
            var lines=new System.Text.StringBuilder();
            foreach(var org in (IEnumerable)await Request("GetOrganizationsAsync"))
            {
                string id=Field(org,"GenesisId");lines.AppendLine("Organization: "+Field(org,"Name")+" | "+id);
                foreach(var project in (IEnumerable)await Request("GetOrganizationProjectsAsync",id))
                    if(Field(project,"Name").IndexOf("monster",StringComparison.OrdinalIgnoreCase)>=0)
                        lines.AppendLine("Existing matching project: "+Field(project,"Name")+" | "+Field(project,"Id"));
            }
            MonsterPouchLocalBridge.Reply("cloud-discovery",lines.ToString());
        }
        catch(Exception error){MonsterPouchLocalBridge.Reply("cloud-discovery","ERROR "+error.GetType().Name+": "+error.Message);}
        finally{busy=false;}
    }
    public static string CreateAndBind(string organizationId)
    {
        if(busy)return "Cloud request already in progress.";
        if(!(bool)Value(Connection,"loggedIn"))return "Unity session is signed out.";
        CreateAsync(organizationId);return "Cloud project creation queued. See cloud-created.txt.";
    }
    static async void CreateAsync(string organizationId)
    {
        busy=true;
        try
        {
            bool found=false;
            foreach(var org in (IEnumerable)await Request("GetOrganizationsAsync"))if(Field(org,"GenesisId")==organizationId)found=true;
            if(!found)throw new InvalidOperationException("Organization is not part of the current session.");
            object project=null;
            foreach(var existing in (IEnumerable)await Request("GetOrganizationProjectsAsync",organizationId))
                if(string.Equals(Field(existing,"Name"),"Monster Pouch",StringComparison.OrdinalIgnoreCase)){project=existing;break;}
            if(project==null)project=await Request("CreateNewProjectInOrganizationAsync",organizationId,"Monster Pouch");
            string id=Field(project,"Id"),name=Field(project,"Name"),legacy=Field(project,"OrganizationLegacyId");
            if(string.IsNullOrEmpty(legacy))legacy=Field(await Request("LegacyGetOrganizationIdAsync",organizationId),"LegacyId");
            if(string.IsNullOrEmpty(id)||string.IsNullOrEmpty(legacy))throw new InvalidOperationException("Cloud returned incomplete project information.");
            var connect=Connection;
            connect.GetType().GetMethod("BindProject",Any).Invoke(connect,new object[]{id,name,legacy});
            connect.GetType().GetMethod("RefreshProject",Any).Invoke(connect,null);
            AssetDatabase.SaveAssets();
            MonsterPouchLocalBridge.Reply("cloud-created","Project: "+name+"\nProject ID: "+id+"\nOrganization: "+organizationId+"\nBound in Unity. Authentication and Relay still need validation.");
        }
        catch(Exception error){MonsterPouchLocalBridge.Reply("cloud-created","ERROR "+error.GetType().Name+": "+error.Message);}
        finally{busy=false;}
    }
    public static void OpenServices()=>SettingsService.OpenProjectSettings("Project/Services");
}
