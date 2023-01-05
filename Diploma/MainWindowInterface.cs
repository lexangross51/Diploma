using System.Windows.Input;

namespace Diploma;

public partial class MainWindow
{
    private void MainWindow_OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
            {
                if (_timeMoment < _timeEnd - 1) _timeMoment++;
                break;
            }
            case Key.Down:
            {
                if (_timeMoment > _timeStart) _timeMoment--;
                break;
            }
        }

        TimeMoment.Text = _timeMoment.ToString();
    }
}