using System.Drawing;
using Amolf.Communication.MessageBased;
using Amolf.Core.Equipment.Lasers;
using Amolf.Core.Graphs;
using Amolf.Core.Graphs.Forms;
using Amolf.Core.Base.Science.Algebra;


const int laser = 2; // 0=405, 1=488, 2=561, 3=638
const int size = 21;
const int delay = 500;



public class PowerMeter : Amolf.Core.Base.Configuration.ConfigurableBase<Communication.MbcConfiguration>
{
   private readonly Communication com;

   public PowerMeter()
   {
     com = new Communication(this);
     var cfg1 = com.Configuration as Communication.MbcConfiguration;
     cfg1.Layer = Communication.LayerType.VISA;
     var cfg2 = cfg1.GetChild("VISA") as Communication.VisaConfiguration;
     cfg2.ResourceName = "USB0::0x1313::0x8078::P0006809::INSTR";
     com.Open();
  }

   public void SetWavelength(int nm) => com.WriteLine($"CORR:WAVE {nm}");

   public double Power => double.Parse(com.Query("Read?"));
   
   public void Close() => com.Close();
}


var lasers = new ILaser[] {Laser405, Laser488, Laser561, Laser638};

var tb = TriggerBox;
var l = lasers[laser];
var laserName = l.FriendlyName;
var wl = (int)l.Wavelength;
var dataX = new double[size];
var dataY = new double[size];
var dataFit = new double[size];
var dr = new DataRange(0,100);
var pm = new PowerMeter();

if ((l.Status & LaserStatus.Emitting) == 0) { Trace.Error("Laser is not Emitting!"); return; };

pm.SetWavelength(wl);
TriggerBox.SetDigital(laser, true);
TriggerBox.SetOverride(true);


GraphData gs1, gs2;
Main.Invoke( (Action)(() => 
{ 
   var gf = new GraphForm(true,true,false) { Text = "Laser Power Check", MinimizeBox = false, MaximizeBox = false, };
   gf.GraphHost.Legend.Location = GraphLegend.eLocation.TopLeft;
   gs1 = gf.GraphHost.AddMarkers(dataX, dataY, laserName, Color.Yellow);   
   gs2 = gf.GraphHost.AddSerie(dataFit, dr, "fit", Color.Red);
   gf.GraphHost.AxisHor.Title = "Control [%]";
   gf.GraphHost.AxisVer.Title = "Power [W]";
   gf.MdiParent = Main; 
   gf.Show();
}));


Trace.WriteLine($"Running {laserName}");


for (int i = 0; i < size; i++)
{
   var step = dr.Interpolate(i, size);
   TriggerBox.SetAnalog(laser, step);
   Delay(delay);
   Cancel.ThrowIfCancellationRequested();

   dataX[i] = step;
   dataY[i] = pm.Power;
   Trace.WriteLine($"{step}\t{dataY[i]:F6}");
   gs1.DataChanged();
   gs1.TopLevel.RedrawGraph();
}
TriggerBox.SetAnalog(laser, 0);
pm.Close();


var max = dataY.Max();
Trace.WriteLine($"Maximum Power: {(max * 1E3):F1} mW");


ALMath.LinearLeastSquaresFit(dataX, dataY, out var a, out var b, true, out var sa, out var sb);
Trace.Info($"Calibration Offset: {a:F6}, Scale: {b:F6}");
Trace.Info($"SigmaFit Offset: {sa:F6}, Scale: {sb:F6}");

for (int i = 0; i < size; i++) 
   dataFit[i] = a + b * dr.Interpolate(i, size);

gs2.DataChanged();
gs2.TopLevel.RedrawGraph();

