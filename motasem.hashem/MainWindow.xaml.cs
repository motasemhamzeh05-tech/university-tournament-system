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
using System.IO;
using System.Xml.Serialization;

namespace motasem.hashem
{
    public partial class MainWindow : Window
    {

        // ===== Persistence DTOs =====
        public class SerializableTeam
        {
            public string Name { get; set; }
            public int Points { get; set; }
            public List<string> Members { get; set; }
            public int Events { get; set; }
            public bool SingleEvent { get; set; }
        }

        public class SerializableIndividual
        {
            public string Name { get; set; }
            public int Points { get; set; }
            public int Events { get; set; }
            public bool SingleEvent { get; set; }
        }

        public class SystemData
        {
            public List<SerializableTeam> Teams { get; set; }
            public List<SerializableIndividual> Individuals { get; set; }
        }

        // ===== Limits =====
        const int MAX_TEAMS = 4;
        const int MAX_MEMBERS_PER_TEAM = 5;
        const int MAX_INDIVIDUALS = 20;
        const int MAX_EVENTS_PER_PARTICIPANT = 5;

        // ===== Points System =====
        Dictionary<int, int> pointsTable = new Dictionary<int, int>()
        {
            {1, 10},
            {2, 7},
            {3, 5},
            {4, 3},
            {5, 1}
        };

        // ===== Data Storage =====
        Dictionary<string, int> teamPoints = new Dictionary<string, int>();
        Dictionary<string, int> individualPoints = new Dictionary<string, int>();

        // team -> list of members
        Dictionary<string, List<string>> teamMembers = new Dictionary<string, List<string>>();

        // events completed counts
        Dictionary<string, int> teamEventCounts = new Dictionary<string, int>();
        Dictionary<string, int> individualEventCounts = new Dictionary<string, int>();

        // single-event flags
        Dictionary<string, bool> teamSingleEvent = new Dictionary<string, bool>();
        Dictionary<string, bool> individualSingleEvent = new Dictionary<string, bool>();

        // last selected team cache to avoid transient null SelectedItem
        private string lastSelectedTeam = null;

        public MainWindow()
        {
            InitializeComponent();
            // try load saved data; if none, prepopulate default teams
            if (!LoadSystem())
                PrepopulateTeams();

            UpdateCounts();

            // save on close
            this.Closing += MainWindow_Closing;
        }

        // ================= PERSISTENCE =================
        private string GetDataFilePath()
        {
            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MotasemHashem");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return System.IO.Path.Combine(dir, "systemdata.xml");
        }

        private void SaveSystem()
        {
            try
            {
                var data = new SystemData()
                {
                    Teams = teamPoints.Select(kvp => new SerializableTeam
                    {
                        Name = kvp.Key,
                        Points = kvp.Value,
                        Members = teamMembers.ContainsKey(kvp.Key) ? teamMembers[kvp.Key] : new List<string>(),
                        Events = teamEventCounts.ContainsKey(kvp.Key) ? teamEventCounts[kvp.Key] : 0,
                        SingleEvent = teamSingleEvent.ContainsKey(kvp.Key) ? teamSingleEvent[kvp.Key] : false
                    }).ToList(),
                    Individuals = individualPoints.Select(kvp => new SerializableIndividual
                    {
                        Name = kvp.Key,
                        Points = kvp.Value,
                        Events = individualEventCounts.ContainsKey(kvp.Key) ? individualEventCounts[kvp.Key] : 0,
                        SingleEvent = individualSingleEvent.ContainsKey(kvp.Key) ? individualSingleEvent[kvp.Key] : false
                    }).ToList()
                };

                string path = GetDataFilePath();
                using (var fs = File.Open(path, FileMode.Create))
                {
                    var ser = new XmlSerializer(typeof(SystemData));
                    ser.Serialize(fs, data);
                }
            }
            catch
            {
                // ignore persistence errors for now
            }
        }

        private bool LoadSystem()
        {
            try
            {
                string path = GetDataFilePath();
                if (!File.Exists(path)) return false;

                SystemData data;
                using (var fs = File.OpenRead(path))
                {
                    var ser = new XmlSerializer(typeof(SystemData));
                    data = (SystemData)ser.Deserialize(fs);
                }

                // clear existing
                teamPoints.Clear();
                individualPoints.Clear();
                teamMembers.Clear();
                teamEventCounts.Clear();
                individualEventCounts.Clear();
                teamSingleEvent.Clear();
                individualSingleEvent.Clear();
                lstTeams.Items.Clear();
                lstIndividuals.Items.Clear();

                if (data?.Teams != null)
                {
                    foreach (var t in data.Teams)
                    {
                        teamPoints[t.Name] = t.Points;
                        teamMembers[t.Name] = t.Members ?? new List<string>();
                        teamEventCounts[t.Name] = t.Events;
                        teamSingleEvent[t.Name] = t.SingleEvent;
                        lstTeams.Items.Add(t.Name);
                    }
                }

                if (data?.Individuals != null)
                {
                    foreach (var ind in data.Individuals)
                    {
                        individualPoints[ind.Name] = ind.Points;
                        individualEventCounts[ind.Name] = ind.Events;
                        individualSingleEvent[ind.Name] = ind.SingleEvent;
                        lstIndividuals.Items.Add(ind.Name);
                    }
                }

                if (lstTeams.Items.Count > 0)
                {
                    lstTeams.SelectedIndex = 0;
                    lastSelectedTeam = lstTeams.Items[0].ToString();
                }

                UpdateResults();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSystem();
        }

        // ================= DELETE INDIVIDUAL =================
        private void DeleteIndividual_Click(object sender, RoutedEventArgs e)
        {
            if (lstIndividuals.SelectedItem == null)
            {
                MessageBox.Show("Select an individual to delete");
                SetStatus("Please select an individual from the list before clicking Delete.", true);
                return;
            }

            string name = lstIndividuals.SelectedItem.ToString();

            if (MessageBox.Show($"Remove individual '{name}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            if (individualPoints.ContainsKey(name)) individualPoints.Remove(name);
            if (individualEventCounts.ContainsKey(name)) individualEventCounts.Remove(name);
            if (individualSingleEvent.ContainsKey(name)) individualSingleEvent.Remove(name);

            lstIndividuals.Items.Remove(name);
            MessageBox.Show($"Individual '{name}' removed");
            SetStatus($"Individual '{name}' removed.", false);
            UpdateCounts();
            UpdateResults();
        }

        // Pre-create the fixed teams so user cannot enter team names
        private void PrepopulateTeams()
        {
            // clear any existing
            teamPoints.Clear();
            teamMembers.Clear();
            teamEventCounts.Clear();
            teamSingleEvent.Clear();
            lstTeams.Items.Clear();

            // create 4 predefined teams
            for (int i = 1; i <= MAX_TEAMS; i++)
            {
                string teamName = $"Team {i}";
                teamPoints[teamName] = 0;
                teamMembers[teamName] = new List<string>();
                teamEventCounts[teamName] = 0;
                teamSingleEvent[teamName] = false;
                lstTeams.Items.Add(teamName);
            }

            if (lstTeams.Items.Count > 0)
            {
                lstTeams.SelectedIndex = 0;
                lastSelectedTeam = lstTeams.Items[0].ToString();
                SetStatus("Teams initialized. Select a team to add members.", false);
            }
        }

        private void SetStatus(string message, bool isError)
        {
            var tb = this.FindName("txtStatus") as TextBlock;
            if (tb != null)
            {
                tb.Text = message;
                tb.Foreground = isError ? Brushes.Red : Brushes.Green;
            }
        }

        private void UpdateCounts()
        {
            // update team/individual counts and enable/disable buttons using FindName to avoid designer field issues
            var lblTeamCount = this.FindName("lblTeamCount") as TextBlock;
            var lblIndividualCount = this.FindName("lblIndividualCount") as TextBlock;
            var lblTeamMembersCount = this.FindName("lblTeamMembersCount") as TextBlock;
            var btnAddTeam = this.FindName("btnAddTeam") as Button;
            var btnAddIndividual = this.FindName("btnAddIndividual") as Button;
            var btnAddMember = this.FindName("btnAddMember") as Button;

            if (lblTeamCount != null)
                lblTeamCount.Text = $"Teams: {teamPoints.Count}/{MAX_TEAMS}";
            if (lblIndividualCount != null)
                lblIndividualCount.Text = $"Individuals: {individualPoints.Count}/{MAX_INDIVIDUALS}";

            if (btnAddTeam != null)
                btnAddTeam.IsEnabled = teamPoints.Count < MAX_TEAMS;
            if (btnAddIndividual != null)
                btnAddIndividual.IsEnabled = individualPoints.Count < MAX_INDIVIDUALS;

            // members for selected team
            string team = (lstTeams.SelectedItem != null) ? lstTeams.SelectedItem.ToString() : lastSelectedTeam;
            int membersCount = 0;
            if (!string.IsNullOrEmpty(team) && teamMembers.ContainsKey(team))
                membersCount = teamMembers[team].Count;

            if (lblTeamMembersCount != null)
                lblTeamMembersCount.Text = $"Members: {membersCount}/{MAX_MEMBERS_PER_TEAM}";

            if (btnAddMember != null)
                btnAddMember.IsEnabled = !string.IsNullOrEmpty(team) && membersCount < MAX_MEMBERS_PER_TEAM;
        }

        // ================= ADD TEAM =================
        private void AddTeam_Click(object sender, RoutedEventArgs e)
        {
            // Teams are predefined in this application and cannot be added by the user.
            MessageBox.Show("Teams are predefined and cannot be added.");
        }

        // ================= ADD MEMBER TO TEAM =================
        private void AddMember_Click(object sender, RoutedEventArgs e)
        {
            string member = txtMemberName.Text.Trim();
            if (string.IsNullOrEmpty(member))
            {
                MessageBox.Show("Enter member name");
                SetStatus("Enter member name before adding. Example: 'Ahmed Ali'", true);
                return;
            }
            // determine selected team: prefer SelectedItem, fall back to SelectedIndex, then lastSelectedTeam
            string team = null;
            if (lstTeams.SelectedItem != null)
            {
                team = lstTeams.SelectedItem.ToString();
            }
            else if (lstTeams.SelectedIndex >= 0)
            {
                team = lstTeams.Items[lstTeams.SelectedIndex].ToString();
            }
            else if (!string.IsNullOrEmpty(lastSelectedTeam))
            {
                team = lastSelectedTeam;
            }

            if (string.IsNullOrEmpty(team))
            {
                MessageBox.Show("Select a team first to add a member");
                return;
            }
            if (!teamMembers.ContainsKey(team))
            {
                MessageBox.Show("Selected team not found");
                return;
            }

            var members = teamMembers[team];
            if (members.Contains(member))
            {
                MessageBox.Show("Member already in team");
                SetStatus($"'{member}' is already in team '{team}'.", true);
                return;
            }

            if (members.Count >= MAX_MEMBERS_PER_TEAM)
            {
                MessageBox.Show($"A team can have at most {MAX_MEMBERS_PER_TEAM} members.");
                SetStatus($"Cannot add more members to '{team}'. Max {MAX_MEMBERS_PER_TEAM} reached.", true);
                return;
            }

            members.Add(member);
            MessageBox.Show($"Member '{member}' added to team '{team}'");
            SetStatus($"Member '{member}' added to '{team}'.", false);
            txtMemberName.Clear();
            UpdateCounts();
        }

        // DeleteTeam_Click removed — teams are managed automatically

        private void MemberName_GotFocus(object sender, RoutedEventArgs e)
        {
            if (string.Equals(txtMemberName.Text, "Member name", StringComparison.OrdinalIgnoreCase))
            {
                txtMemberName.Text = string.Empty;
                txtMemberName.Foreground = Brushes.Black;
            }
        }

        private void MemberName_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtMemberName.Text))
            {
                txtMemberName.Text = "Member name";
                txtMemberName.Foreground = Brushes.Gray;
            }
        }
        

        // ================= ADD INDIVIDUAL =================
        private void AddIndividual_Click(object sender, RoutedEventArgs e)
        {
            string name = txtIndividualName.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Enter individual name");
                SetStatus("Enter the individual's name (e.g. 'Mona Saleh') before adding.", true);
                return;
            }

            if (individualPoints.ContainsKey(name))
            {
                MessageBox.Show("Individual already exists");
                SetStatus($"Individual '{name}' already exists.", true);
                return;
            }

            if (individualPoints.Count >= MAX_INDIVIDUALS)
            {
                MessageBox.Show($"Maximum of {MAX_INDIVIDUALS} individual participants allowed.");
                return;
            }

            individualPoints[name] = 0;
            individualEventCounts[name] = 0;
            bool isSingle = (chkIndividualSingleEvent != null && chkIndividualSingleEvent.IsChecked == true);
            individualSingleEvent[name] = isSingle;
            lstIndividuals.Items.Add(name);
            txtIndividualName.Clear();
            if (chkIndividualSingleEvent != null) chkIndividualSingleEvent.IsChecked = false;
            UpdateCounts();
            SetStatus($"Individual '{name}' added.", false);
        }

        private void LstTeams_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstTeams.SelectedItem != null)
            {
                lastSelectedTeam = lstTeams.SelectedItem.ToString();
            }
            UpdateCounts();
        }

        // ================= RESET SYSTEM =================
        private void ResetSystem_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Reset the entire system? This will remove all individuals and scores.", "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            // clear individuals
            individualPoints.Clear();
            individualEventCounts.Clear();
            individualSingleEvent.Clear();
            lstIndividuals.Items.Clear();

            // clear teams and recreate defaults
            PrepopulateTeams();

            // clear position and member input fields
            if (this.FindName("txtPosition") is TextBox pos)
                pos.Text = string.Empty;
            if (this.FindName("txtMemberName") is TextBox mem)
                mem.Text = string.Empty;
            if (this.FindName("txtIndividualName") is TextBox ind)
                ind.Text = string.Empty;

            UpdateCounts();
            UpdateResults();

            MessageBox.Show("System reset completed.");
            SetStatus("System has been reset. All individuals removed and teams reinitialized.", false);
        }

        // ================= RESET RESULTS (teams & individuals points/events) =================
        private void ResetResults_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Reset all results? This will set all points and event counts to zero.", "Confirm Reset Results", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            // Reset team points and event counts
            foreach (var key in teamPoints.Keys.ToList())
                teamPoints[key] = 0;
            foreach (var key in teamEventCounts.Keys.ToList())
                teamEventCounts[key] = 0;

            // Reset individual points and event counts
            foreach (var key in individualPoints.Keys.ToList())
                individualPoints[key] = 0;
            foreach (var key in individualEventCounts.Keys.ToList())
                individualEventCounts[key] = 0;

            UpdateResults();
            UpdateCounts();

            MessageBox.Show("Results have been reset.");
            SetStatus("All results cleared. Points and event counts reset to zero.", false);
        }

        // ================= DELETE RESULTS (remove persisted data file and clear in-memory results) =================
        private void DeleteResults_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Delete all results and remove saved data file? This cannot be undone.", "Confirm Delete Results", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                string path = GetDataFilePath();
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // ignore file delete errors
            }

            // Clear points and event counts in memory
            foreach (var key in teamPoints.Keys.ToList())
                teamPoints[key] = 0;
            foreach (var key in teamEventCounts.Keys.ToList())
                teamEventCounts[key] = 0;

            // Clear individuals' results only (keep names in the main list)
            foreach (var key in individualPoints.Keys.ToList())
                individualPoints[key] = 0;
            foreach (var key in individualEventCounts.Keys.ToList())
                individualEventCounts[key] = 0;
            // keep individualSingleEvent flags and lstIndividuals intact so the main tab entries remain

            UpdateResults();
            UpdateCounts();

            MessageBox.Show("Results deleted and saved data removed (if present).");
            SetStatus("Results deleted and saved data file removed.", false);
        }

        // ================= RECORD RESULT =================
        private void RecordResult_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtPosition.Text, out int position))
            {
                MessageBox.Show("Enter valid position");
                SetStatus("Position must be a number between 1 and 5.", true);
                return;
            }

            if (!pointsTable.ContainsKey(position))
            {
                MessageBox.Show("No points for this position");
                SetStatus("No points configured for this position. Use 1-5.", true);
                return;
            }

            int points = pointsTable[position];

            // Determine event type robustly (use Content or text)
            string eventType = null;
            if (cmbEventType.SelectedItem is ComboBoxItem cbi && cbi.Content != null)
                eventType = cbi.Content.ToString();
            else if (!string.IsNullOrEmpty(cmbEventType.Text))
                eventType = cmbEventType.Text;

            bool isIndividualEvent = string.Equals(eventType, "Individual", StringComparison.OrdinalIgnoreCase) || cmbEventType.SelectedIndex == 0;

            if (isIndividualEvent) // Individual
            {
                if (lstIndividuals.SelectedItem == null)
                {
                    MessageBox.Show("Select individual first");
                    SetStatus("Select an individual from the list before recording a result.", true);
                    return;
                }

                string name = lstIndividuals.SelectedItem.ToString();
                if (!individualPoints.ContainsKey(name))
                {
                    MessageBox.Show("Individual not found");
                    return;
                }

                if (!individualEventCounts.ContainsKey(name)) individualEventCounts[name] = 0;

                if (individualEventCounts[name] >= MAX_EVENTS_PER_PARTICIPANT)
                {
                    MessageBox.Show($"{name} has already completed the maximum of {MAX_EVENTS_PER_PARTICIPANT} events.");
                    return;
                }

                if (individualSingleEvent.ContainsKey(name) && individualSingleEvent[name] && individualEventCounts[name] >= 1)
                {
                    MessageBox.Show($"{name} is registered for a single event only.");
                    return;
                }

                individualPoints[name] += points;
                individualEventCounts[name] += 1;
                SetStatus($"Recorded {points} points to '{name}' for event {cmbEventName.Text}.", false);
            }
            else // Team
            {
                if (lstTeams.SelectedItem == null)
                {
                    MessageBox.Show("Select team first");
                    SetStatus("Select a team from the list before recording a team result.", true);
                    return;
                }

                string name = lstTeams.SelectedItem.ToString();
                if (!teamPoints.ContainsKey(name))
                {
                    MessageBox.Show("Team not found");
                    return;
                }

                if (!teamEventCounts.ContainsKey(name)) teamEventCounts[name] = 0;

                if (teamEventCounts[name] >= MAX_EVENTS_PER_PARTICIPANT)
                {
                    MessageBox.Show($"Team '{name}' has already completed the maximum of {MAX_EVENTS_PER_PARTICIPANT} events.");
                    return;
                }

                if (teamSingleEvent.ContainsKey(name) && teamSingleEvent[name] && teamEventCounts[name] >= 1)
                {
                    MessageBox.Show($"Team '{name}' is registered for a single event only.");
                    return;
                }

                teamPoints[name] += points;
                teamEventCounts[name] += 1;
                SetStatus($"Recorded {points} points to team '{name}' for event {cmbEventName.Text}.", false);
            }

            UpdateResults();
            MessageBox.Show("Result recorded");
        }

        // ================= UPDATE RESULTS =================
        private void UpdateResults()
        {
            dgTeamResults.ItemsSource = null;
            dgIndividualResults.ItemsSource = null;

            dgTeamResults.ItemsSource = teamPoints
                .OrderByDescending(x => x.Value)
                .Select(x => new
                {
                    Team = x.Key,
                    Points = x.Value,
                    Events = (teamEventCounts.ContainsKey(x.Key) ? teamEventCounts[x.Key] : 0),
                    Members = (teamMembers.ContainsKey(x.Key) ? string.Join(", ", teamMembers[x.Key]) : string.Empty)
                });

            dgIndividualResults.ItemsSource = individualPoints
                .OrderByDescending(x => x.Value)
                .Select(x => new
                {
                    Individual = x.Key,
                    Points = x.Value,
                    Events = (individualEventCounts.ContainsKey(x.Key) ? individualEventCounts[x.Key] : 0)
                });
        }
    }
}