using Android.App;
using Android.Widget;
using Android.OS;
using Android.Content;
using Java.Lang;
using Android.Preferences;
using Android.Support.V4.Content;
using Android.Support.V4.App;
using Android;
using Android.Util;
using Android.Support.Design.Widget;
using Android.Net;
using Android.Locations;
using Android.Support.V7.App;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using RestSharp;
using System;
using Android.Telephony;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;

namespace LocUpdFgService
{
    public class BaseResponse
    {
        public string Result;
        public string Description;
    }

    public class JsonResponse : BaseResponse
    {
        public List<dynamic> Records = new List<dynamic>();
    }
    public sealed class CSSingleton
    {
        private static volatile CSSingleton instance = null;
        private static readonly object _thisLock = new object();

        private CSSingleton()
        {
        }

        public static CSSingleton Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }
                lock (_thisLock)
                {
                    if (instance == null)
                    {
                        instance = new CSSingleton();
                    }
                }
                return instance;
            }
        }

        public void SaveConfig(string config_name, Dictionary<string, string> config_data)
        {
            try
            {
                var path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
                var filename = path + "/" + config_name + ".json";
                var json = JsonConvert.SerializeObject(config_data);
                File.WriteAllText(filename, json);
            }
            catch (System.Exception ex)
            {
                Toast.MakeText(Application.Context, ex.Message, ToastLength.Short).Show();
            }
        }

        public Dictionary<string, string> ReadConfig(string config_name)
        {
            Dictionary<string, string> config_data = new Dictionary<string, string>();
            try
            {
                var path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
                var filename = path + "/" + config_name + ".json";
                if (File.Exists(filename))
                {
                    string content;
                    using (var streamReader = new StreamReader(filename))
                    {
                        content = streamReader.ReadToEnd();
                    }
                    config_data = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);
                }
            }
            catch (System.Exception ex)
            {
                Toast.MakeText(Application.Context, ex.Message, ToastLength.Short).Show();
            }
            return config_data;
        }

        public MainActivity MainActivity { get; set; } = null;

        public BroadcastReceiverOTP Receiver { get; set; } = null;

        public void StartReceiver()
        {
            try
            {
                if (Receiver == null)
                {
                    Receiver = new BroadcastReceiverOTP();
                    Application.Context.RegisterReceiver(Receiver, new IntentFilter("android.provider.Telephony.SMS_RECEIVED"));
                }
            }
            catch (System.Exception ex)
            {
                Receiver = null;
                Toast.MakeText(Application.Context, ex.Message, ToastLength.Short).Show();
            }
        }
        public void StopReceiver()
        {
            try
            {
                if (Receiver != null)
                {
                    Application.Context.UnregisterReceiver(Receiver);
                    Receiver = null;
                }
            }
            catch (System.Exception ex)
            {
                Receiver = null;
                Toast.MakeText(Application.Context, ex.Message, ToastLength.Short).Show();
            }
        }

        public JsonResponse MakeRequest(Dictionary<string, string> parameters = null, string api_url = "")
        {
            var result = new JsonResponse()
            {
                Result = "OK",
                Description = "OK",
                Records = new List<dynamic>()
            };
            try
            {
                if (System.String.IsNullOrEmpty(api_url))
                {
                    api_url = "http://api.smsforward.getondomain.com/Apismsforward/queue";
                }
                var client = new RestClient(api_url);
                var request = new RestRequest("/", Method.POST);
                request.AddHeader("Accept", "application/json");
                if (parameters != null)
                {
                    var l = parameters.Count;
                    foreach (KeyValuePair<string, string> entry in parameters)
                    {
                        if (!request.Parameters.Exists(m => m.Name == entry.Key))
                        {
                            request.AddParameter(entry.Key, entry.Value);
                        }
                    }
                }
                IRestResponse response = client.Execute(request);
                var content = response.Content;
                result = JsonConvert.DeserializeObject<JsonResponse>(content);
            }
            catch (System.Exception ex)
            {
                result.Result = "ERROR";
                result.Description = ex.Message;
            }
            return result;
        }
    }

    [BroadcastReceiver(Enabled = true)]
    //[IntentFilter(new[] { "android.provider.Telephony.SMS_RECEIVED" }, Priority = (int)IntentFilterPriority.HighPriority)]
    public class BroadcastReceiverOTP : BroadcastReceiver
    {
        public static readonly string INTENT_ACTION = "android.provider.Telephony.SMS_RECEIVED";

        [Obsolete]
        public override void OnReceive(Context context, Intent intent)
        {
            try
            {
                if (intent.HasExtra("pdus"))
                {
                    var smsArray = (Java.Lang.Object[])intent.Extras.Get("pdus");
                    var config_name = "app_config";
                    Dictionary<string, string> config_data = CSSingleton.Instance.ReadConfig(config_name);
                    if (!config_data.ContainsKey("redirect_emails") || !config_data.ContainsKey("redirect_active") || System.String.IsNullOrEmpty(config_data["redirect_emails"]) || config_data["redirect_active"] != "1")
                    {
                        return;
                    }
                    foreach (var item in smsArray)
                    {
                        try
                        {
                            TelephonyManager mTelephonyMgr = Application.Context.GetSystemService(Context.TelephonyService) as TelephonyManager;
                            var phone = mTelephonyMgr.Line1Number;
                            var sms = SmsMessage.CreateFromPdu((byte[])item);
                            var IndexOnIcc = sms.TimestampMillis;
                            var sender = sms.OriginatingAddress;
                            var message = sms.MessageBody;
                            Dictionary<string, string> parameters = new Dictionary<string, string>
                            {
                                { "phone", phone },
                                { "sender", sender },
                                { "message", message },
                                { "IndexOnIcc", IndexOnIcc.ToString() },
                                { "redirect_emails", config_data["redirect_emails"] }
                            };
                            CSSingleton.Instance.MainActivity.SendNotificationToJavascript("callback_sms_received", parameters);
                            var response = CSSingleton.Instance.MakeRequest(parameters);
                            var response_str = JsonConvert.SerializeObject(response, Formatting.Indented);
                            parameters = new Dictionary<string, string>
                            {
                                { "action", "email sent" },
                                { "response", response_str }
                            };
                            CSSingleton.Instance.MainActivity.SendNotificationToJavascript("callback_sms_received", parameters);
                        }
                        catch (System.Exception ex1)
                        {
                            Toast.MakeText(Application.Context, ex1.Message, ToastLength.Short).Show();
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Toast.MakeText(Application.Context, ex.Message, ToastLength.Short).Show();
            }
        }
    }


    /**
	 * The only activity in this sample.
	 *
	 * Note: for apps running in the background on "O" devices (regardless of the targetSdkVersion),
	 * location may be computed less frequently than requested when the app is not in the foreground.
	 * Apps that use a foreground service -  which involves displaying a non-dismissable
	 * notification -  can bypass the background location limits and request location updates as before.
	 *
	 * This sample uses a long-running bound and started service for location updates. The service is
	 * aware of foreground status of this activity, which is the only bound client in
	 * this sample. After requesting location updates, when the activity ceases to be in the foreground,
	 * the service promotes itself to a foreground service and continues receiving location updates.
	 * When the activity comes back to the foreground, the foreground service stops, and the
	 * notification associated with that foreground service is removed.
	 *
	 * While the foreground service notification is displayed, the user has the option to launch the
	 * activity from the notification. The user can also remove location updates directly from the
	 * notification. This dismisses the notification and stops the service.
	 */
    [Activity(Label = "LocUpdFgService", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, ISharedPreferencesOnSharedPreferenceChangeListener
    {
        const string Tag = "MainActivity";

        // Used in checking for runtime permissions.
        const int RequestPermissionsRequestCode = 34;

        public string config_name = "app_config";

        // The BroadcastReceiver used to listen from broadcasts from the service.
        MyReceiver myReceiver;

        // A reference to the service used to get location updates.
        LocationUpdatesService Service;

        // Tracks the bound state of the service.
        bool Bound;

        // UI elements.
        Button RequestLocationUpdatesButton;
        Button RemoveLocationUpdatesButton;
        CheckBox RedirectActive;
        EditText RedirectEmails;

        // Monitors the state of the connection to the service.
        CustomServiceConnection ServiceConnection;
        public bool CheckConnectionToAddress(string address)
        {
            var result = false;
            try
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    IPHostEntry host = Dns.GetHostEntry(address);
                    if (host != null)
                    {
                        foreach (IPAddress ip in host.AddressList)
                        {
                            Ping pingSender = new Ping();
                            PingReply reply = pingSender.Send(ip);
                            if (reply.Status == IPStatus.Success || reply.Status == IPStatus.TtlExpired)
                            {
                                result = true;
                                break;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Toast.MakeText(Application.Context, ex.Message, ToastLength.Short).Show();
                result = false;
            }
            return result;
        }
        public void SendNotificationToJavascript(string callback, Dictionary<string, string> parameters)
        {
            try
            {
                var parameters_str = JsonConvert.SerializeObject(parameters, Formatting.Indented);
                var bytes = Encoding.UTF8.GetBytes(parameters_str);
                var base64 = Convert.ToBase64String(bytes);
                var js = $"{callback}('{base64}');";
                //webView.LoadUrl("javascript:" + js);
            }
            catch (System.Exception ex)
            {
                Toast.MakeText(Application.Context, ex.Message, ToastLength.Short).Show();
            }
        }
        public int GetSmsId(string phone, string sms)
        {
            var inboxURI = Android.Net.Uri.Parse("content://sms/inbox");

            var where = string.Format(" address = '{0}'", phone);

            var cursor = ContentResolver.Query(inboxURI, null, where, null, " _id desc ");

            var id = -1;

            if (cursor != null && cursor.Count > 0)
            {
                var found = false;
                while (!found && cursor.MoveToNext())
                {
                    var _id = cursor.GetString(cursor.GetColumnIndex("_id"));

                    var body = cursor.GetString(cursor.GetColumnIndex("body"));

                    if (System.String.Equals(body, sms, StringComparison.InvariantCulture))
                    {
                        found = true;

                        id = Convert.ToInt32(_id);
                    }
                }
            }

            return id;
        }
        class CustomServiceConnection : Java.Lang.Object, IServiceConnection
        {
            public MainActivity Activity { get; set; }
            public void OnServiceConnected(ComponentName name, IBinder service)
            {
                LocationUpdatesServiceBinder binder = (LocationUpdatesServiceBinder)service;
                Activity.Service = binder.GetLocationUpdatesService();
                Activity.Bound = true;
            }

            public void OnServiceDisconnected(ComponentName name)
            {
                Activity.Service = null;
                Activity.Bound = false;
            }
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            CSSingleton.Instance.MainActivity = this;

            myReceiver = new MyReceiver { Context = this };
            ServiceConnection = new CustomServiceConnection { Activity = this };

            SetContentView(Resource.Layout.activity_main);

            // Check that the user hasn't revoked permissions by going to Settings.
            if (Utils.RequestingLocationUpdates(this))
            {
                if (!CheckPermissions())
                {
                    RequestPermissions();
                }
            }
        }

        protected override void OnStart()
        {
            base.OnStart();

            PreferenceManager.GetDefaultSharedPreferences(this).RegisterOnSharedPreferenceChangeListener(this);

            RequestLocationUpdatesButton = FindViewById(Resource.Id.request_location_updates_button) as Button;
            RemoveLocationUpdatesButton = FindViewById(Resource.Id.remove_location_updates_button) as Button;
            RedirectActive = FindViewById(Resource.Id.redirect_active) as CheckBox;
            RedirectEmails = FindViewById(Resource.Id.redirect_emails) as EditText;

            RequestLocationUpdatesButton.Click += (sender, e) =>
            {
                if (!CheckPermissions())
                {
                    RequestPermissions();
                }
                else
                {
                    Service.RequestLocationUpdates();
                }
            };

            RemoveLocationUpdatesButton.Click += (sender, e) =>
            {
                Service.RemoveLocationUpdates();
            };

            RedirectActive.Click += (sender, e) =>
            {
                try
                {
                    var active = RedirectActive.Checked;
                    Dictionary<string, string> config_data = CSSingleton.Instance.ReadConfig(config_name);
                    if (!System.String.IsNullOrEmpty(RedirectEmails.Text) && active)
                    {
                        config_data["redirect_active"] = "1";
                        CSSingleton.Instance.StartReceiver();
                    }
                    else
                    {
                        RedirectActive.Checked = false; ;
                        config_data["redirect_active"] = "0";
                        CSSingleton.Instance.StopReceiver();
                    }
                    config_data["redirect_emails"] = RedirectEmails.Text;
                    CSSingleton.Instance.SaveConfig(config_name, config_data);
                }
                catch (System.Exception ex)
                {
                    Toast.MakeText(Application.Context, ex.Message, ToastLength.Short).Show();
                }
            };

            Dictionary<string, string> config_data = CSSingleton.Instance.ReadConfig(config_name);
            if (!config_data.ContainsKey("redirect_active"))
            {
                config_data.Add("redirect_active", "0");
            }
            if (!config_data.ContainsKey("redirect_emails"))
            {
                config_data.Add("redirect_emails", "");
            }
            CSSingleton.Instance.SaveConfig(config_name, config_data);
            RedirectEmails.Text = config_data["redirect_emails"];
            if (!System.String.IsNullOrEmpty(config_data["redirect_emails"]) && config_data["redirect_active"] == "1")
            {
                RedirectActive.Checked = true;
                CSSingleton.Instance.StartReceiver();
            }
            else
            {
                RedirectActive.Checked = false;
                CSSingleton.Instance.StopReceiver();
            }            

            // Restore the state of the buttons when the activity (re)launches.
            SetButtonsState(Utils.RequestingLocationUpdates(this));

            // Bind to the service. If the service is in foreground mode, this signals to the service
            // that since this activity is in the foreground, the service can exit foreground mode.
            BindService(new Intent(this, typeof(LocationUpdatesService)), ServiceConnection, Bind.AutoCreate);
        }

        protected override void OnResume()
        {
            base.OnResume();
            LocalBroadcastManager.GetInstance(this).RegisterReceiver(myReceiver,
                new IntentFilter(LocationUpdatesService.ActionBroadcast));
        }

        protected override void OnPause()
        {
            LocalBroadcastManager.GetInstance(this).UnregisterReceiver(myReceiver);
            base.OnPause();
        }

        protected override void OnStop()
        {
            if (Bound)
            {
                // Unbind from the service. This signals to the service that this activity is no longer
                // in the foreground, and the service can respond by promoting itself to a foreground
                // service.
                UnbindService(ServiceConnection);
                Bound = false;
            }
            PreferenceManager.GetDefaultSharedPreferences(this)
                    .UnregisterOnSharedPreferenceChangeListener(this);
            base.OnStop();
        }

        /**
	     * Returns the current state of the permissions needed.
	     */
        bool CheckPermissions()
        {
            return PermissionChecker.PermissionGranted == ContextCompat.CheckSelfPermission(this,Manifest.Permission.AccessFineLocation);
        }

        void RequestPermissions()
        {
            var shouldProvideRationale = ActivityCompat.ShouldShowRequestPermissionRationale(this,Manifest.Permission.AccessFineLocation);

            // Provide an additional rationale to the user. This would happen if the user denied the
            // request previously, but didn't check the "Don't ask again" checkbox.
            if (shouldProvideRationale)
            {
                Log.Info(Tag, "Displaying permission rationale to provide additional context.");
                Snackbar.Make(
                        FindViewById(Resource.Id.activity_main),
                        Resource.String.permission_rationale,
                        Snackbar.LengthIndefinite)
                        .SetAction(Resource.String.ok, (obj) =>
                        {
                            // Request permission
                            ActivityCompat.RequestPermissions(this,
                                    new string[] { Manifest.Permission.AccessFineLocation },
                                    RequestPermissionsRequestCode);
                        })
                        .Show();
            }
            else
            {
                Log.Info(Tag, "Requesting permission");
                // Request permission. It's possible this can be auto answered if device policy
                // sets the permission in a given state or the user denied the permission
                // previously and checked "Never ask again".
                ActivityCompat.RequestPermissions(this,
                        new string[] { Manifest.Permission.AccessFineLocation },
                        RequestPermissionsRequestCode);
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions,
                         Android.Content.PM.Permission[] grantResults)
        {
            Log.Info(Tag, "onRequestPermissionResult");
            if (requestCode == RequestPermissionsRequestCode)
            {
                if (grantResults.Length <= 0)
                {
                    // If user interaction was interrupted, the permission request is cancelled and you
                    // receive empty arrays.
                    Log.Info(Tag, "User interaction was cancelled.");
                }
                else if (grantResults[0] == PermissionChecker.PermissionGranted)
                {
                    // Permission was granted.
                    Service.RequestLocationUpdates();
                }
                else
                {
                    // Permission denied.
                    SetButtonsState(false);
                    Snackbar.Make(
                            FindViewById(Resource.Id.activity_main),
                            Resource.String.permission_denied_explanation,
                            Snackbar.LengthIndefinite)
                            .SetAction(Resource.String.settings, (obj) =>
                            {
                                // Build intent that displays the App settings screen.
                                Intent intent = new Intent();
                                intent.SetAction(Android.Provider.Settings.ActionApplicationDetailsSettings);
                                var uri = Android.Net.Uri.FromParts("package", PackageName, null);
                                intent.SetData(uri);
                                intent.SetFlags(ActivityFlags.NewTask);
                                StartActivity(intent);
                            })
                            .Show();
                }
            }
        }

        /**
	     * Receiver for broadcasts sent by {@link LocationUpdatesService}.
	     */
        class MyReceiver : BroadcastReceiver
        {
            public Context Context { get; set; }
            public override void OnReceive(Context context, Intent intent)
            {
                var location = intent.GetParcelableExtra(LocationUpdatesService.ExtraLocation) as Location;
                if (location != null)
                {
                    Toast.MakeText(Context, Utils.GetLocationText(location), ToastLength.Short).Show();
                }

            }
        }

        public void OnSharedPreferenceChanged(ISharedPreferences sharedPreferences, string key)
        {
            // Update the buttons state depending on whether location updates are being requested.
            if (key.Equals(Utils.KeyRequestingLocationUpdates))
            {
                SetButtonsState(sharedPreferences.GetBoolean(Utils.KeyRequestingLocationUpdates, false));
            }
        }

        void SetButtonsState(bool requestingLocationUpdates)
        {
            if (requestingLocationUpdates)
            {
                RequestLocationUpdatesButton.Enabled = false;
                RemoveLocationUpdatesButton.Enabled = true;
            }
            else
            {
                RequestLocationUpdatesButton.Enabled = true;
                RemoveLocationUpdatesButton.Enabled = false;
            }
        }
    }
}

