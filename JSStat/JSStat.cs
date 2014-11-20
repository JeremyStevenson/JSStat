using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

// JSStat by Jeremy Stevenson 11/14/2014
// jeremynstevenson@gmail.com

namespace JSStat
{
  public class Account
  {
    public static String LoginURL = "https://mytotalconnectcomfort.com/portal/";
    public static String LocationsURL = "https://mytotalconnectcomfort.com/portal/Locations";
    public static String ZonesURL = "https://mytotalconnectcomfort.com/portal/";
    public static String DeviceControlURL = "https://mytotalconnectcomfort.com/portal/Device/Control/";

    private String username;
    private String password;
    private CookieContainer cookies;
    private DateTime lastLoginTime;
    private static int timeoutMinutes = 5;

    public List<Location> Locations = new List<Location>();

    public Account( String username, String password )
    {
      this.username = username;
      this.password = password;
    }

    internal CookieContainer Login()
    {
      System.Net.HttpWebRequest request;
      System.Net.HttpWebResponse response;
      StreamReader streamReader;
      String responseString;

      // Check to see if we are already logged in and it hasn't timed out.
      int cookieAgeMinutes = (int)DateTime.Now.Subtract( lastLoginTime ).TotalMinutes;
      if( cookies != null && cookieAgeMinutes < timeoutMinutes )
      {
//        Tools.Log( "Cookie age is " + cookieAgeMinutes + " minutes, which is less than timeout of " + timeoutMinutes + "minutes. Using existing cookies." );
        return cookies;
      }

      // If not already logged in, locate the saved cookies file. 
      try
      {
        cookieAgeMinutes = (int)DateTime.Now.Subtract( File.GetLastWriteTime( "JSStatCookies" ) ).TotalMinutes;
        if( cookieAgeMinutes < timeoutMinutes )
        {
  //        Tools.Log( "Saved cookies file age is " + cookieAgeMinutes + " minutes, which is less than timeout of " + timeoutMinutes + "minutes. Using existing cookies." );
          FileStream stream = new FileStream( "JSStatCookies", FileMode.Open );
          BinaryFormatter formatter = new BinaryFormatter();
          cookies = (CookieContainer)formatter.Deserialize( stream );
          stream.Close();
          return cookies;
        }
      }
      catch( Exception ex )
      {
        Tools.Log( ex.Message );
      }

      Tools.Log( "Cookies are expired. Logging in again." );

      cookies = new System.Net.CookieContainer();

      //GET login page and initial cookies
      request = (HttpWebRequest)System.Net.HttpWebRequest.Create( LoginURL );
      request.KeepAlive = true;
      request.CookieContainer = cookies;
      request.Method = "GET";
      request.ContentType = "application/x-www-form-urlencoded";
      request.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/33.0.1750.154 Safari/537.36";

      response = (HttpWebResponse)request.GetResponse();
      streamReader = new StreamReader( response.GetResponseStream() );
      responseString = streamReader.ReadToEnd();

      cookies.Add( response.Cookies );

      //Login and get authorization cookies
      request = (HttpWebRequest)System.Net.HttpWebRequest.Create( LoginURL );
      request.CookieContainer = cookies;
      request.Method = "POST";
      request.KeepAlive = true;
      request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
      request.ContentType = "application/x-www-form-urlencoded";
      request.Referer = LoginURL;
      request.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/33.0.1750.154 Safari/537.36";
      string postData = "timeOffset=240&UserName=" + username + "&Password=" + password + "&RememberMe=false";
      byte[] byteArray = System.Text.Encoding.UTF8.GetBytes( postData );
      request.ContentLength = byteArray.Length;
      System.IO.Stream dataStream = request.GetRequestStream();
      dataStream.Write( byteArray, 0, byteArray.Length );
      dataStream.Close();

      // Get the response.
      response = (HttpWebResponse)request.GetResponse();
      dataStream = response.GetResponseStream();
      streamReader = new System.IO.StreamReader( dataStream );
      responseString = streamReader.ReadToEnd();
      streamReader.Close();
      dataStream.Close();
      response.Close();

//      Tools.Log( "Login response string " + responseString );


      // check that the login was succesful
      if( !responseString.Contains( "My Locations" ) )
        throw new Exception( "Login failed. Check username and password." );

      cookies.Add( response.Cookies );

      // write login cookies to disk
      try
      {
        FileStream stream = new FileStream( "JSStatCookies", FileMode.Create );
        BinaryFormatter formatter = new BinaryFormatter();
        formatter.Serialize( stream, cookies );
        stream.Close();
      }
      catch( Exception ex )
      {
        Tools.Log( "Error writting cookies to disk. " + ex.ToString() );
      }

      lastLoginTime = DateTime.Now;
      return cookies;
    }

    void GetLocations()
    {
      //GET Locations page
      System.Net.HttpWebRequest request;
      System.Net.HttpWebResponse response;
      StreamReader streamReader;
      String responseString;

      request = (HttpWebRequest)WebRequest.Create( LocationsURL );
      request.KeepAlive = true;
      request.CookieContainer = Login();
      request.Method = "GET";
      request.ContentType = "application/x-www-form-urlencoded";
      request.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/33.0.1750.154 Safari/537.36";

      response = (HttpWebResponse)request.GetResponse();
      streamReader = new StreamReader( response.GetResponseStream() );
      responseString = streamReader.ReadToEnd();
      response.Close();

      // pull out all "data-id=" values for locations
      int index = 1;
      while( true )
      {
        String locationID = Tools.Parse( responseString, "data-id=\"", "\" data-url", ref index );
        if( locationID == null ) break;
        String locationName = Tools.Parse( responseString, "<div class=\"location-name\">", "</div>", ref index ).Trim();
        Tools.Log( "Location ID: " + locationID + ", Location Name: " + locationName );
        Locations.Add( new Location( this, locationID, locationName ) );
      }
    }
  }

  public class Location
  {
    public String ID;
    public String Name;
    public Account Account;
    public List<Zone> Zones = new List<Zone>();

    public Location( Account account, String id, String name )
    {
      ID = id;
      Name = name;
      Account = account;
    }

    public void GetZones()
    {
      //GET Zones page
      System.Net.HttpWebRequest request;
      System.Net.HttpWebResponse response;
      StreamReader streamReader;
      String responseString;

      request = (HttpWebRequest)WebRequest.Create( Account.ZonesURL + ID + "/Zones" );
      request.KeepAlive = true;
      request.CookieContainer = Account.Login();
      request.Method = "GET";
      request.ContentType = "application/x-www-form-urlencoded";
      request.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/33.0.1750.154 Safari/537.36";

      response = (HttpWebResponse)request.GetResponse();
      streamReader = new StreamReader( response.GetResponseStream() );
      responseString = streamReader.ReadToEnd();
      response.Close();

      // pull out all "data-id=" values
      int index = 1;
      string thermostatID = null;
      string thermostatName = null;
      while( true )
      {
        thermostatID = Tools.Parse( responseString, "data-id=\"", "\" data-url", ref index );
        if( thermostatID == null )
          break; // TODO: might not be correct. Was : Exit Do
        thermostatName = Tools.Parse( responseString, "<div class=\"location-name\">", "</div>", ref index ).Trim();
        Tools.Log( "Thermostat ID: " + thermostatID + ", Thermostat Name: " + thermostatName );
        Zones.Add( new Zone( Account, thermostatID, thermostatName ) );
      }
    }
  }
  
  public class Zone
  {
    public Account Account;
    public String ID;
    public String Name;

    public enum SystemSwitchEnum { Off = 2, Heat = 1, Cool = 3}
    public enum ScheduleEnum { FollowSchedule = 0, HoldUntil = 1, PermanentHold = 2 }

    public int CoolSetPoint;
    public int HeatSetPoint;
    public String Temperature;
    public SystemSwitchEnum SystemSwitch;
    public ScheduleEnum Schedule;
    public int NextPeriod;

    public Zone( Account account, String id, String name )
    {
      Account = account;
      ID = id;
      Name = name;
    }

    public void Update()
    {
      //GET Control page
      System.Net.HttpWebRequest request;
      System.Net.HttpWebResponse response;
      StreamReader streamReader;
      String responseString;

      request = (HttpWebRequest)System.Net.HttpWebRequest.Create( Account.DeviceControlURL + ID );
      request.KeepAlive = true;
      request.CookieContainer = Account.Login();
      request.Method = "GET";
      request.ContentType = "application/x-www-form-urlencoded";
      request.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/33.0.1750.154 Safari/537.36";

      response = (HttpWebResponse)request.GetResponse();
      streamReader = new StreamReader( response.GetResponseStream() );
      responseString = streamReader.ReadToEnd();
      response.Close();

      Tools.Log( responseString );

      // pull out data  
      CoolSetPoint = (int)double.Parse( Tools.Parse( responseString, "coolSetpoint, ", ");" ) );
      //Tools.Log( "coolSetPoint=" + CoolSetPoint );
      HeatSetPoint = (int)double.Parse( Tools.Parse( responseString, "heatSetpoint, ", ");" ) );
      //Tools.Log( "heatSetPoint=" + HeatSetPoint );
      Temperature = Tools.Parse( responseString, "dispTemperature, ", ");" );
      //Tools.Log( "temperature=" + Temperature );

      SystemSwitch = (SystemSwitchEnum)int.Parse( Tools.Parse( responseString, "systemSwitchPosition, ", ");" ) );
      //Tools.Log( "SystemSwitch=" + SystemSwitch.ToString() );

      Schedule = (ScheduleEnum)int.Parse( Tools.Parse( responseString, "statusHeat, ", ");" ) );
      //Tools.Log( "Schedule=" + Schedule.ToString() );

      if( SystemSwitch == SystemSwitchEnum.Heat )
      {
        NextPeriod = int.Parse( Tools.Parse( responseString, "heatNextPeriod, ", ");" ) );
        //Tools.Log( "NextPeriod=" + NextPeriod );
      }
      if( SystemSwitch == SystemSwitchEnum.Cool )
      {
        NextPeriod = int.Parse( Tools.Parse( responseString, "coolNextPeriod, ", ");" ) );
        //Tools.Log( "NextPeriod=" + NextPeriod );
      }

    }



/* 
-- off -> set cool permanent hold heat at 50
CoolNextPeriod: null
CoolSetpoint: null
DeviceID: 416280
FanMode: null
HeatNextPeriod: null
HeatSetpoint: 51
StatusCool: 2
StatusHeat: 2
SystemSwitch: 1

--> off
CoolNextPeriod: null
CoolSetpoint: null
DeviceID: 416280
FanMode: null
HeatNextPeriod: null
HeatSetpoint: null
StatusCool: null
StatusHeat: null
SystemSwitch: 2
 
--> heat hold until 8:15 pm
CoolNextPeriod: 81
CoolSetpoint: null
DeviceID: 416280
FanMode: null
HeatNextPeriod: 81
HeatSetpoint: 52
StatusCool: 1
StatusHeat: 1
SystemSwitch: 1
  
--> follow schedule
CoolNextPeriod: null
CoolSetpoint: 99
DeviceID: 416280
FanMode: null
HeatNextPeriod: null
HeatSetpoint: 50
StatusCool: 0
StatusHeat: 0
SystemSwitch: null
*/ 

    /// <summary>
    /// Control the thermostat
    /// </summary>
    /// <param name="systemSwitch">Off, Heat, Cool</param>
    /// <param name="schedule">FollowSchedule, HoldUntil, PermanentHold</param>
    /// <param name="setPoint">Set poitn for heating or cooling</param>
    /// <param name="holdHour">Hours to hold for. E.g. 2 holds for 2 hours.</param>
    /// <param name="holdMinute">Minutes to hold for in multiples of 15 minutes. E.g. 2 = 30 minutes</param>
    public void Set( SystemSwitchEnum systemSwitch, ScheduleEnum schedule, int setPoint, int nextPeriod )
    {
      System.Net.HttpWebRequest request;
      System.Net.HttpWebResponse response;
      StreamReader streamReader;
      String responseString;

      request = (HttpWebRequest)System.Net.HttpWebRequest.Create( "https://mytotalconnectcomfort.com/portal/Device/SubmitControlScreenChanges" );
      request.KeepAlive = false;
      request.ProtocolVersion = HttpVersion.Version10;
      request.CookieContainer = Account.Login();
      request.Method = "POST";
      request.Accept = "application/json, text/javascript, */*";
      request.ContentType = "application/json; charset=UTF-8";
      request.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/33.0.1750.154 Safari/537.36";
      string postData = "{ \"DeviceID\":" + ID;
      postData += ", \"CoolNextPeriod\":" + ((schedule == ScheduleEnum.HoldUntil) ? nextPeriod.ToString() : "null");
      postData += ", \"CoolSetPoint\":" + ((systemSwitch == SystemSwitchEnum.Cool) ? setPoint.ToString() : "null");
      postData += ", \"FanMode\":null";
      postData += ", \"HeatNextPeriod\":" + ((schedule == ScheduleEnum.HoldUntil) ? nextPeriod.ToString() : "null");
      postData += ", \"HeatSetPoint\":" + ((systemSwitch == SystemSwitchEnum.Heat) ? setPoint.ToString() : "null");
      postData += ", \"StatusCool\":" + (int)schedule;
      postData += ", \"StatusHeat\":" + (int)schedule;
      postData += ", \"SystemSwitch\":" + (int)systemSwitch;
      postData += "}";
      byte[] byteArray = System.Text.Encoding.UTF8.GetBytes( postData );
      request.ContentLength = byteArray.Length;
      System.IO.Stream dataStream = request.GetRequestStream();
      dataStream.Write( byteArray, 0, byteArray.Length );
      dataStream.Close();

      response = (HttpWebResponse)request.GetResponse();
      streamReader = new StreamReader( response.GetResponseStream() );
      responseString = streamReader.ReadToEnd();
      response.Close();
    }

    /// <summary>
    /// Control the thermostat
    /// </summary>
    /// <param name="systemSwitch">Off, Heat, Cool</param>
    /// <param name="setPoint">Set poitn for heating or cooling</param>
    /// <param name="holdHour">Hours to hold for. E.g. 2 holds for 2 hours.</param>
    /// <param name="holdMinute">Minutes to hold for in multiples of 15 minutes. E.g. 2 = 30 minutes</param>
    public void SetHoldUntil( SystemSwitchEnum systemSwitch, int setPoint, int holdHour, int holdMinute )
    {
      int nextPeriod = (DateTime.Now.Hour + holdHour) * 4 + (int)Math.Ceiling(DateTime.Now.Minute / 15.0) + holdMinute;
      Set( systemSwitch, ScheduleEnum.HoldUntil, setPoint, nextPeriod );
    }
  }

  public class Tools
  {
    public static String Parse( string s, string front, string back )
    {
      int index = 0;
      return Parse( s, front, back, ref index );
    }

    public static String Parse( string s, string front, string back, ref int index )
    {
      int length = front.Length;
      int startpos = s.IndexOf( front, index );
      if( startpos == -1 )
        return null;
      int endpos = s.IndexOf( back, startpos + length );
      if( endpos == -1 )
        return null;
      index = endpos + back.Length;
      return s.Substring( startpos + length, endpos - (startpos + length) );
    }

    public static String XMLParse( string xml, string field )
    {
      String startpt = "<" + field + ">"; ;
      String endpt = "</" + field + ">";
      int length = startpt.Length;
      int length2 = endpt.Length;
      int startpos = xml.IndexOf( startpt );
      int position = xml.IndexOf( startpt, startpos );
      int endpos = xml.IndexOf( endpt );
      return xml.Substring( position + length, endpos - (position + length) );
    }

    static internal void Log( String log )
    {
      System.Console.Out.WriteLine( log );
    }
  }
}
