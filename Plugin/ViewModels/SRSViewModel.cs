using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using System.Windows.Input;
using System.Windows;
using System.Collections.ObjectModel;
using Plugin.Models;
using System.Reflection;
using System.Globalization;
using System.Windows.Data;

namespace Plugin
{
    public class SRSViewModel : ViewModelBase
    {
        private ContextModel _contextModel;

        public SRSViewModel(ContextModel contextModel)
        {
            _contextModel = contextModel;
            List<Structure> targetStructures = _contextModel.Context.ExternalPlanSetup.StructureSet.Structures.Where(
                struc => (struc.DicomType == "PTV" || struc.DicomType == "GTV")
            ).ToList();

            // Populate the list of selectable targets with our target structures
            _selectableTargets = new List<StructureRow>();
            foreach (Structure s in targetStructures)
            {
                _selectableTargets.Add(new StructureRow() { structure = s, isChecked = false });
            }

            //ImagePath45 = GetPath("Head_A.png");
            //ImagePath60 = GetPath("Head_E.png");
            SelectedTabIndex = 1; // 45 deg
            TabsVisible = true;

            // Set default checkbox values!
            AutomateIso = true;
            OptimiseCol = true;

        }

        //private string GetPath(string resource)
        //{
        //    return "pack://application:,,,/" + Assembly.GetExecutingAssembly().GetName().Name + ";component/Resources/" + resource;
        //}

        
        private string _imagePath45;
        public string ImagePath45
        {
            get => _imagePath45;
            set => Set(ref _imagePath45, value);
        }
        private string _imagePath60;
        public string ImagePath60
        {
            get => _imagePath60;
            set => Set(ref _imagePath60, value);
        }


        public class StructureRow
        {
            public Structure structure { get; set; }
            public bool isChecked { get; set; }
        }

        private List<StructureRow> _selectableTargets;
        public List<StructureRow> SelectableTargets
        {
            get => _selectableTargets;
            set => Set(ref _selectableTargets, value);
        }

        public ICommand ArrangementRadioCommand => new RelayCommand<string>(radioButtonClick);
        private void radioButtonClick(string name)
        {
            if (name == "ExistButton")
            {
                SelectedTabIndex = 0;
                TabsVisible = false;
            }

            else if (name == "45degButton")
            { 
                SelectedTabIndex = 1;
                TabsVisible = true;
            }
            else if (name == "60degButton")
            { 
                SelectedTabIndex = 2;
                TabsVisible = true;
            }
        }

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => Set(ref _selectedTabIndex, value);
        }

        private bool _isSelected45DegT0L = true;
        public bool IsSelected45DegT0L
        {
            get => _isSelected45DegT0L;
            set => Set(ref _isSelected45DegT0L, value);
        }

        private bool _isSelected45DegT45 = true;
        public bool IsSelected45DegT45
        {
            get => _isSelected45DegT45;
            set => Set(ref _isSelected45DegT45, value);
        }

        private bool _isSelected45DegT90 = true;
        public bool IsSelected45DegT90
        {
            get => _isSelected45DegT90;
            set => Set(ref _isSelected45DegT90, value);
        }

        private bool _isSelected45DegT315 = true;
        public bool IsSelected45DegT315
        {
            get => _isSelected45DegT315;
            set => Set(ref _isSelected45DegT315, value);
        }

        private bool _isSelected45DegT0R = true;
        public bool IsSelected45DegT0R
        {
            get => _isSelected45DegT0R;
            set => Set(ref _isSelected45DegT0R, value);
        }

        private bool _isSelected60DegT0L = true;
        public bool IsSelected60DegT0L
        {
            get => _isSelected60DegT0L;
            set => Set(ref _isSelected60DegT0L, value);
        }

        private bool _isSelected60DegT60 = true;
        public bool IsSelected60DegT60
        {
            get => _isSelected60DegT60;
            set => Set(ref _isSelected60DegT60, value);
        }

        private bool _isSelected60DegT300 = true;
        public bool IsSelected60DegT300
        {
            get => _isSelected60DegT300;
            set => Set(ref _isSelected60DegT300, value);
        }

        private bool _isSelected60DegT0R = true;
        public bool IsSelected60DegT0R
        {
            get => _isSelected60DegT0R;
            set => Set(ref _isSelected60DegT0R, value);
        }

        private bool _automateIso;
        public bool AutomateIso
        {
            get => _automateIso;
            set => Set(ref _automateIso, value);
        }

        private bool _optimiseCol;
        public bool OptimiseCol
        {
            get => _optimiseCol;
            set => Set(ref _optimiseCol, value);
        }

        public ICommand CalculateOptimalCollimatorCommand => new RelayCommand(CalculateOptimalCollimator);
        private async void CalculateOptimalCollimator()
        {
            _contextModel.SelectedTargets = _selectableTargets.Where(s => s.isChecked).Select(s => s.structure).ToList();
            if (_contextModel.SelectedTargets.Count == 0)
            {
                MessageBox.Show("Invalid selection");
                return;
            }

            _contextModel.CreatePlanNew();
            // TODO: fix the below. First get your geometry from your arrangement viewmodel (based on the selected arrangement)
            List<SimpleBeam> fieldGeometry = new List<SimpleBeam>();
            if (SelectedTabIndex == 0)
            {
                // do nothing! (existing geometry)
            }
            else if (SelectedTabIndex == 1) // 45° ROtations
            {
                if (IsSelected45DegT0R)
                    fieldGeometry.Add(new SimpleBeam(ArcLen.Half, 180.1, 0, GantryDirection.Clockwise, 0, "01_T0"));
                if (IsSelected45DegT0L)
                    fieldGeometry.Add(new SimpleBeam(ArcLen.Half, 0, 179.9, GantryDirection.Clockwise, 0, "02_T0"));
                if (IsSelected45DegT45)
                    fieldGeometry.Add(new SimpleBeam(ArcLen.Half, 179.9, 0, GantryDirection.CounterClockwise, 315, "03_T45"));
                if (IsSelected45DegT90)
                    fieldGeometry.Add(new SimpleBeam(ArcLen.Half, 0, 179.9, GantryDirection.Clockwise, 270, "04_T90"));
                if (IsSelected45DegT315)
                    fieldGeometry.Add(new SimpleBeam(ArcLen.Half, 0, 180.1, GantryDirection.CounterClockwise, 45, "05_T315"));
            }
            else if (SelectedTabIndex == 2) // 60° ROtations
            {
                if (IsSelected60DegT0R)
                    fieldGeometry.Add(new SimpleBeam(ArcLen.Half, 180.1, 0, GantryDirection.Clockwise, 0, "01_T0"));
                if (IsSelected60DegT0L)
                    fieldGeometry.Add(new SimpleBeam(ArcLen.Half, 0, 179.9, GantryDirection.Clockwise, 0, "02_T0"));
                if (IsSelected60DegT300)
                    fieldGeometry.Add(new SimpleBeam(ArcLen.Half, 179.9, 0, GantryDirection.CounterClockwise, 300, "03_T60"));
                if (IsSelected60DegT60)
                    fieldGeometry.Add(new SimpleBeam(ArcLen.Half, 0, 180.1, GantryDirection.CounterClockwise, 60, "04_T300"));
            }
                

            if (AutomateIso)
                _contextModel.AddBeams(IsoPlacement.BoundingSphere, fieldGeometry);
            else
                _contextModel.AddBeams(IsoPlacement.PreviousPlan, fieldGeometry);

            if (OptimiseCol)
                await _contextModel.SetOptimalCollimator(new EventHandler<int>(OnProgressChanged));

            
            
            System.Windows.Application.Current.MainWindow.Close();
        }


        private int _progress;
        public int Progress
        {
            get => _progress;
            set => Set(ref _progress, value);
        }

        private void OnProgressChanged(object sender, int progress)
        {
            Progress = progress;
        }


        public bool _tabsVisible;
        public bool TabsVisible
        {
            get => _tabsVisible;
            set => Set(ref _tabsVisible, value);
        }

        

    }
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool && (bool)value)
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed; // or Visibility.Hidden
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility && (Visibility)value == Visibility.Visible)
            {
                return true;
            }
            return false;
        }
    }
}
