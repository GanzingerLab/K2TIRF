if (Focus.Lock) { Trace.Error("Cannot move when FocusLock is engaged!"); return; }

const double centerPos = 823.9;       // in um
const double delta     =   2;
const double step      =   0.1;

var pos = 0.0;
var delay = 100 / (4*delta/step);
Trace.Info($"Delay = {delay} ms");
bool direction = true;
while (true)
{
   CheckAbort();
   
   if (direction)
   {
      if (pos + step < delta) pos += step;     // go up
      else direction = !direction;
   }
   else
   {
      if (pos - step > -delta) pos -= step;    // go down
      else direction = !direction;
   }
   
   MotorZ.MoveAbsolute(centerPos + pos);
   Delay((int)delay);
}