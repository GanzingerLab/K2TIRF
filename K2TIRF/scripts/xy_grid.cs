const double centerPosX = -1638.6;       // in um
const double centerPosY = 1869.8;
const double delta     =  140;
const double step      =   10;

var posX = 0.0;
var posY = 0.0;
var delay = 3000;
Trace.Info($"Delay = {delay} ms");
bool direction = true;

while (posX + step < delta) 
{
   posX += step;     // go X
   MotorX.MoveAbsolute(centerPosX + posX);
   Delay((int)delay);
   if (direction)
      {
         while (posY + step < delta)
         {
            posY += step;     // go Y
            MotorY.MoveAbsolute(centerPosY + posY);
            Delay((int)delay);
         }
         direction = !direction;
      }
   else
      {
          while (posY + step > 0.0)
         {
            posY -= step;     // go Y
            MotorY.MoveAbsolute(centerPosY + posY);
            Delay((int)delay);
         }
         direction = !direction;
      }
}