using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Net;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.ComponentModel;
using System.Xml;

namespace JSStat
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {

    bool debugRun = false;
    bool alwaysPulse = false;
    int pulseTemp = 72;

    public MainWindow()
    {
      InitializeComponent();
    }

    private readonly BackgroundWorker worker = new BackgroundWorker();

    private void Window_Loaded( object sender, RoutedEventArgs e )
    {
      worker.DoWork += pulseHeat;
      worker.RunWorkerAsync();
    }

    private void pulseHeat( object sender, DoWorkEventArgs e )
    {
      log( "Loading thermostats." );

      Account account = new Account( "**username**", "**password**" );
      Location location = new Location( account, "**defualt location id**", "**defualt location**" );
      location.GetZones();

      while( true )
      {
        // if it's not cold months, go to sleep for another day.
        if( DateTime.Now.Month > 3 && DateTime.Now.Month < 10 ) 
        {
          log( "It's not November, December, January, February, or March. Not pulsing heat. Sleeping for 1 day." );
          Thread.Sleep( 1000 * 60 * 60 * 24 );
          continue;
        }

        // look up outside temperature and windchill
        try
        {
          // get the tempearture and windchill
          WebClient client = new WebClient();
          string reply = client.DownloadString( "http://w1.weather.gov/xml/current_obs/KBED.xml" );
          XmlDocument doc = new XmlDocument();
          doc.LoadXml( reply );
          float outsideTemperature = float.Parse( doc.GetElementsByTagName( "temp_f" )[0].InnerText );
          float outsideWindchill = outsideTemperature;
          try
          {
            outsideWindchill = float.Parse( doc.GetElementsByTagName( "windchill_f" )[0].InnerText );
          }
          catch( Exception ) { }

          if( !alwaysPulse && !debugRun && outsideTemperature > 29.0 && (outsideTemperature > 31 || outsideWindchill > 25.0) )
          {
            log( "Nothing to do. Outside Temperature = " + outsideTemperature + " Wind Chill = " + outsideWindchill );
            log( "Sleeping for 30 minutes." );
            Thread.Sleep( (debugRun ? 10 : 1000) * 60 * 30 );
            continue;
          }
          log( "Brrrr... it's cold. Outside Temperature = " + outsideTemperature + " Wind Chill = " + outsideWindchill );
        }
        catch( Exception e2 )
        {
          log( "Error looking up temperature, pulsing heat just in case.", LogType.Important );
          log( e2 );
        }

        // Pulse the heat every hour when it's cold outside.
        System.DateTime sleepUntil = DateTime.Now.AddMinutes( debugRun ? 1 : 60 );

        // if we got here, it's cold outside. Loop through zones pulsing temperature.
        foreach( Zone zone in location.Zones )
        {
          try
          {
            log( "Found thermostat = " + zone.Name + ". Polling data." );
            zone.Update();
            log( "Thermostat current set point is " + zone.HeatSetPoint + " mode is " + zone.Schedule.ToString() );

            if( zone.HeatSetPoint > 65 )
            {
              log( "Found thermostat = " + zone.Name + " current set point is " + zone.HeatSetPoint + ". No need to pulse heat when set point is > 65." );
              continue;
            }

            log( "Pulsing heat for thermostat " + zone.Name, LogType.Important );

            // save the zone values
            Zone.ScheduleEnum schedule = zone.Schedule;
            int heatSetpoint = zone.HeatSetPoint;
            int nextPeriod = zone.NextPeriod;

            zone.SetHoldUntil( Zone.SystemSwitchEnum.Heat, pulseTemp, 0, 1 );
            log( "Sleeping for 7 minutes." );
            Thread.Sleep( (debugRun ? 10 : 1000) * 60 * 7 );
            log( "Returning zone to previous settings." );
            zone.Set( Zone.SystemSwitchEnum.Heat, schedule, heatSetpoint, nextPeriod );
          }
          catch( Exception e3 )
          {
            log( e3 );
          }

        }

        log( "Sleeping unitl next hour is up." );
        Thread.Sleep( (sleepUntil - DateTime.Now) );
      }
    }

    FlowDocument logFlowDocument;
    int lineCount = 0;
    enum LogType { Normal, Important, Error };

    private void log( String message )
    {
      log( message, LogType.Normal );
    }

    private void log( Exception e )
    {
      log( e.ToString(), LogType.Error );
      log( e.StackTrace.ToString(), LogType.Error );
    }

    private void log( String message, LogType logType )
    {
      if( !richTextBox.Dispatcher.CheckAccess() )
      {
        richTextBox.Dispatcher.Invoke(
          System.Windows.Threading.DispatcherPriority.Normal,
          new Action(
            delegate() { log( message, logType ); }
          ) 
        );
        return;
      }

      // clear the log every 1,000 entries.
      lineCount++;
      if( lineCount > 1000 )
      {
        lineCount = 0;
        logFlowDocument = null;
      }

      if( logFlowDocument == null )
      {
        logFlowDocument = new FlowDocument();
        Style style = new Style( typeof( Paragraph ) );
        style.Setters.Add( new Setter( Block.MarginProperty, new Thickness( 0 ) ) );
        logFlowDocument.Resources.Add( typeof( Paragraph ), style );
        richTextBox.Document = logFlowDocument;
      }

      message = DateTime.Now.ToShortTimeString() + " " + message;
      Paragraph p;
      if( logType == LogType.Important ) 
        p = new Paragraph( new Bold( new Run( message ) ) );
      else
        p = new Paragraph( new Run( message ) );
      if( logType == LogType.Error )
      p.Foreground = Brushes.Red;
      logFlowDocument.Blocks.Add( p );
      richTextBox.ScrollToEnd();
     
    }

  }
}