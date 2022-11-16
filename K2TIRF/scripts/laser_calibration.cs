using System.Drawing;
using Amolf.Communication.MessageBased;
using Amolf.Core.Equipment.Lasers;
using Amolf.Core.Graphs;
using Amolf.Core.Graphs.Forms;
using Amolf.Core.Base.Science.Algebra;
using Amolf.Core.Base.Science.Numerical;
using Amolf.Equipment.K2.TriggerBox;


const int laser = 1;          // 0=405, 1=488, 2=561, 3=631
const int size = 51;          // nr of points
const int delay = 500;        // readout delay
const int order = 8;          // polynomial order 
const double threshold = 1E-3;      // 1 mW


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
var dataFX = new double[100];
var dataFY = new double[100];
var dr = new DataRange(0,4095);
var pm = new PowerMeter();

if ((l.Status & LaserStatus.Emitting) == 0) { Trace.Error("Laser is not Emitting!"); return; };

pm.SetWavelength(wl);
TriggerBox.SetDigital(laser, true);
TriggerBox.SetOverride(true);


GraphDataDouble1DArrPairedLimited gs1;
GraphData gs2;
Main.Invoke( (Action)(() => 
{ 
   var gf = new GraphForm(true,true,false) { Text = "Laser Calibration", MinimizeBox = false, MaximizeBox = false, };
   var gam = new GraphArtistMarker(gf.GraphHost.Artist) { DrawSerieAboveAxis = true, DrawLine = false };
   gf.GraphHost.Legend.Location = GraphLegend.eLocation.TopLeft;
   gs1 = new GraphDataDouble1DArrPairedLimited(gam) { Name = laserName, XData = dataX, YData = dataY, Limit = 0};
   gam.SetMarkerForChild(gs1, new GraphMarkerTriangle { MarkerColor = Color.Yellow, MarkerExtend = new Size(2, 2), });
   gs2 = gf.GraphHost.AddSerie(dataFX, dataFY, "fit", Color.Red);
   
   gf.GraphHost.AxisHor.Title = "Control [dac]";
   gf.GraphHost.AxisVer.Title = "Power [W]";
   gf.GraphHost.Artist.PlotRange = new DataRange2D(dr, DataRange.Empty);
   gf.GraphHost.Artist.AutoScaleX = false;
   gf.MdiParent = Main; 
   gf.Show();
}));

Trace.WriteLine($"Running {laserName}");

for (int i = 0; i < size; i++)
{
   var step = (int)dr.Interpolate(i, size);
   TriggerBox.SetAnalogRaw(laser, step);
   
   Delay(delay);
   Cancel.ThrowIfCancellationRequested();

   dataX[i] = step;
   dataY[i] = pm.Power;
   
   Trace.WriteLine($"{step}\t{dataY[i]:G3}");
   gs1.Limit = i+1;
   gs1.DataChanged();
   gs1.TopLevel.RedrawGraph();
}
TriggerBox.SetAnalogRaw(laser, 0);
pm.Close();


var max = dataY.Select( (v,i) => (v,i)).Aggregate( (a,b) => a.v > b.v ? a : b);
Trace.WriteLine($"Maximum Power: {(max.v * 1E3):F1} mW at index {max.i}");

var n = Array.FindIndex(dataY, d => d > threshold);
Trace.WriteLine($"Threshold index: {n}");

// rescale 0-100%, skip below threshold
var dataP = dataY.Skip(n-1).Take(max.i - n).Select(d => d * 100.0 / max.v).ToArray();
var dataD = dataX.Skip(n-1).Take(max.i - n).ToArray();

// initial linear fit
ALMath.LinearLeastSquaresFit(dataP, dataD, out var la, out var lb, true, out var sla, out var slb);
Trace.Info($"Calibration Offset: {la:F6}, Scale: {lb:F6}");
Trace.Info($"SigmaFit Offset: {sla:F6}, Scale: {slb:F6}");


// polynomial fit
var a = new double[order];
a[0] = la;
a[1] = lb;
for (var i=2; i<order; i++) a[i] = 0;
var err = CurveFit.Fit(dataP, dataD, a, CurveFit.funcPolynomial);

for (var i=0; i<order; i++)
   Trace.Info($"Param: {i}, {a[i]:G}");

// plot fit
for (var i=0; i<dataFX.Length; i++) 
{
   dataFX[i] = CurveFit.Polynomial(i, a);
   dataFY[i] = i  * max.v / 100.0;
}

gs2.DataChanged();
gs2.TopLevel.RedrawGraph();

// set to config
var cfg = TriggerBox.Configuration as LaserTriggerBox.Config;
cfg.Calibration[laser] = a;
