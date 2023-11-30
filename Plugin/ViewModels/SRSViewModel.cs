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

            // Populate the list of field arrangements and associated images
            Arrangements = new ObservableCollection<FieldArrangement>
            {
                new FieldArrangement { DisplayName = "Existing Geometry", ImagePath = GetPath("Existing.png")},
                new FieldArrangement { DisplayName = "A: 2×Half, 1×[45,90,315]", ImagePath = GetPath("Head_A.png") },
                new FieldArrangement { DisplayName = "B: 2×Full, 1×[45,90,315]", ImagePath = GetPath("Head_B.png")},
                new FieldArrangement { DisplayName = "C: 2×Full, 1×[60,300]", ImagePath = GetPath("Head_C.png") },
                new FieldArrangement { DisplayName = "D: 2×Full, 2×[60,300]", ImagePath = GetPath("Head_D.png") },
                new FieldArrangement { DisplayName = "E: 2×Half, 1×[60,300]", ImagePath = GetPath("Head_E.png") },
                new FieldArrangement { DisplayName = "F: 2×Half, 2×[60,300]", ImagePath = GetPath("Head_F.png") },
            };
            SelectedArrangement = Arrangements[1];

            // Set default checkbox values!
            AutomateIso = true;
            OptimiseCol = true;
        }

        private string GetPath(string resource)
        {
            return "pack://application:,,,/" + Assembly.GetExecutingAssembly().GetName().Name + ";component/Resources/" + resource;
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
                SelectedTabIndex = 0;
            else if (name == "45degButton")
                SelectedTabIndex = 1;
            else if (name == "60degButton")
                SelectedTabIndex = 2;
        }

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => Set(ref _selectedTabIndex, value);
        }

        private bool _isSelected45DegT0L;
        public bool IsSelected45DegT0L
        {
            get => _isSelected45DegT0L;
            set => Set(ref _isSelected45DegT0L, value);
        }

        private bool _isSelected45DegT45;
        public bool IsSelected45DegT45
        {
            get => _isSelected45DegT45;
            set => Set(ref _isSelected45DegT45, value);
        }

        private bool _isSelected45DegT90;
        public bool IsSelected45DegT90
        {
            get => _isSelected45DegT90;
            set => Set(ref _isSelected45DegT90, value);
        }

        private bool _isSelected45DegT315;
        public bool IsSelected45DegT315
        {
            get => _isSelected45DegT315;
            set => Set(ref _isSelected45DegT315, value);
        }

        private bool _isSelected45DegT0R;
        public bool IsSelected45DegT0R
        {
            get => _isSelected45DegT0R;
            set => Set(ref _isSelected45DegT0R, value);
        }

        private bool _isSelected60DegT0L;
        public bool IsSelected60DegT0L
        {
            get => _isSelected60DegT0L;
            set => Set(ref _isSelected60DegT0L, value);
        }

        private bool _isSelected60DegT60;
        public bool IsSelected60DegT60
        {
            get => _isSelected60DegT60;
            set => Set(ref _isSelected60DegT60, value);
        }

        private bool _isSelected60DegT300;
        public bool IsSelected60DegT300
        {
            get => _isSelected60DegT300;
            set => Set(ref _isSelected60DegT300, value);
        }

        private bool _isSelected60DegT0R;
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
                fieldGeometry = null;
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
            else // 60° ROtations
            {
                if (IsSelected60DegT0R)
                    fieldGeometry.Add(new SimpleBeam());
                if (IsSelected60DegT0L)
                    fieldGeometry.Add(new SimpleBeam());
                if (IsSelected60DegT60)
                    fieldGeometry.Add(new SimpleBeam());
                if (IsSelected60DegT300)
                    fieldGeometry.Add(new SimpleBeam());
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

    }
}
