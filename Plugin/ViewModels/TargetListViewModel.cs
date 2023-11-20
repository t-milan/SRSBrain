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
    public class TargetListViewModel : ViewModelBase
    {
        private ContextModel _contextModel;

        public TargetListViewModel(ContextModel contextModel)
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

        private ObservableCollection<FieldArrangement> _arrangements;
        public ObservableCollection<FieldArrangement> Arrangements
        {
            get => _arrangements;
            set => Set(ref _arrangements, value);
        }

        private FieldArrangement _selectedArrangement;
        public FieldArrangement SelectedArrangement
        {
            get => _selectedArrangement;
            set => Set(ref _selectedArrangement, value);
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
            if (AutomateIso)
                _contextModel.AddBeams(IsoPlacement.BoundingSphere, SelectedArrangement.Geometry());
            else
                _contextModel.AddBeams(IsoPlacement.PreviousPlan, SelectedArrangement.Geometry());

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
