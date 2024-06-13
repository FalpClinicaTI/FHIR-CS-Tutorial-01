using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System;
using System.Collections;
using System.Collections.Generic;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;



namespace FHIR_CS_Tutorial_01;

public partial class frmPaciente : Form
{
    private static readonly Dictionary<string, string> _fhirServers = new Dictionary<string, string>()
    {
      {"PublicVonk", "http://vonk.fire.ly"},
      {"PublicHAPI", "http://hapi.fhir.org/baseR4/"},
      {"Local", "http://localhost:8081/fhir"},
    };

    private static readonly string _fhirServer = _fhirServers["Local"];
    private FhirClient fhirClient;


    public frmPaciente()
    {
        InitializeComponent();
        //Actualizó a la versión 5.8.1 - .NET 8.0
        fhirClient = new FhirClient(_fhirServer)
        {
            Settings = new FhirClientSettings
            {
                PreferredFormat = ResourceFormat.Json,
                ReturnPreference = ReturnPreference.Representation,
                UseAsync = true
            }
        };
    }

    private async void frmPaciente_Load(object sender, EventArgs e)
    {
        List<Patient> patients = await GetPatientsAsync();
    }

    /// <summary>
    /// Obtenga una lista de pacientes que coinciden con los criterios especificados
    /// </summary>
    /// <param name="fhirClient"></param>
    /// <param name="patientCriteria"></param>
    /// <param name="maxPatients">El número máximo de pacientes para regresar (predeterminado: 20)</param>
    /// <param name="onlyWithEncounters">Marcar para devolver solo pacientes con Encuentros(predeterminado: falso)</param>
    /// <returns></returns>
    private async Task<List<Patient>> GetPatientsAsync(
            string[]? patientCriteria = null,
            int maxPatients = 20,
            bool onlyWithEncounters = false)
    {
        var patients = new List<Patient>();
        Bundle? patientBundle = patientCriteria == null || patientCriteria.Length == 0
             ? await fhirClient.SearchAsync<Patient>()
             : await fhirClient.SearchAsync<Patient>(patientCriteria); ;

        while (patientBundle != null && patients.Count < maxPatients)
        {
            textBox1.Text = $"Cantidad de Pacientes: {patientBundle.Total} recuento de entradas: {patientBundle.Entry.Count}";
            //Console.WriteLine($"Cantidad de Pacientes: {patientBundle.Total} recuento de entradas: {patientBundle.Entry.Count}");
            var newPatients = patientBundle.Entry
                    .Where(entry => entry.Resource is Patient)
                    .Select(entry => (Patient)entry.Resource)
                    .ToList();

            foreach (var patient in newPatients)
            {
                if (onlyWithEncounters)
                {
                    var encounterBundle = await fhirClient.SearchAsync<Encounter>(new[] { $"patient=Patient/{patient.Id}" });

                    if (encounterBundle?.Total == 0)
                        continue;
                    textBox2.Text = $" - total de encuentros: {encounterBundle?.Total} recuento de entradas: {encounterBundle?.Entry.Count}";
                    //Console.WriteLine($" - total de encuentros: {encounterBundle?.Total} recuento de entradas: {encounterBundle?.Entry.Count}");
                }

                patients.Add(patient);
                textBox2.Text = $"- Entidades {patients.Count,3}: {patient.Id}";
                textBox2.Text = $" -   Id: {patient.Id}";

                if (patient.Name.Any())
                    textBox2.Text = $" - Nombre: {patient.Name.First()}";

                if (patients.Count >= maxPatients)
                    break;
            }

            if (patients.Count >= maxPatients)
                break;

            // get more results
            patientBundle = await fhirClient.ContinueAsync(patientBundle);
        }
        return patients;
    }

    /// <summary>
    /// Create a patient with the specified name
    /// </summary>
    /// <param name="fhirClient"></param>
    /// <param name="familyName"></param>
    /// <param name="givenName"></param>
    private async System.Threading.Tasks.Task CreatePatientAsync(FhirClient fhirClient, string familyName, string givenName)
    {
        var toCreate = new Patient()
        {
            Name = new List<HumanName>()
            {
                new HumanName()
                {
                    Family = familyName,
                    Given = new List<string>()
                    {
                        givenName,
                    },
                }
            },
            BirthDateElement = new Date(dtpFecNac.Value.ToString("yyyy-MM-dd"))
        };

        var created = await fhirClient.CreateAsync(toCreate);
        MessageBox.Show($"Paciente Creado /{created?.Id}");

    }

    private async void btnGrabar_Click(object sender, EventArgs e)
    {
        try
        {
            await CreatePatientAsync(fhirClient, txtFamilia.Text, txtNombre.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
        LimpiarText();
    }

    /// <summary>
    /// Leer a un paciente de un servidor FHIR, por id
    /// </summary>
    /// <param name="fhirClient"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    static async Task<Patient> ReadPatientAsync(FhirClient fhirClient, string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentNullException(nameof(id));
        }

        var patient = await fhirClient.ReadAsync<Patient>($"Patient/{id}");
        return patient ?? throw new Exception("Paciente no existe en la BD");
    }

    private async void btnBuscar_Click(object sender, EventArgs e)
    {
        try
        {
            var patient = await ReadPatientAsync(fhirClient, txtId.Text);


            patient.Gender = AdministrativeGender.Unknown;

            txtTelefono.Text = patient.Telecom.First().Value;
            textBox2.Text = $" - Nombre: {patient.Name.First()}";
            txtId.Text = patient.Id;
            txtFamilia.Text = patient.Name.First().Family;
            txtNombre.Text = patient.Name.First().Given.First();
            dtpFecNac.Value = SetPatientBirthDate(patient.BirthDateElement);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    /// <summary>
    /// Elimina un Paciente especifico por Id
    /// </summary>
    /// <param name="fhirClient"></param>
    /// <param name="id"></param>
    static async System.Threading.Tasks.Task DeletePatientAsync(FhirClient fhirClient, string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentNullException(nameof(id));
        }
        await fhirClient.DeleteAsync($"Patient/{id}");
    }

    private async void btnEliminar_Click(object sender, EventArgs e)
    {
        try
        {
            await DeletePatientAsync(fhirClient, txtId.Text);
            MessageBox.Show($"Paciente con Id: {txtId.Text} fue eliminado!");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
        LimpiarText();
    }

    private void LimpiarText()
    {
        txtId.Text = "";
        txtFamilia.Text = "";
        txtNombre.Text = "";
        dtpFecNac.Value = DateTime.Now;
        txtTelefono.Text = "";
    }

    private DateTime SetPatientBirthDate(Date fechaNac)
    {
        if (fechaNac != null)
        {
            string strFecha = fechaNac.ToDate()?.Years + "/" + fechaNac.ToDate()?.Months + "/" + fechaNac.ToDate()?.Days;
            if (DateTime.TryParse(strFecha, out DateTime parsedDate))
                return parsedDate;
        }
        return DateTime.Now;
    }

    /// <summary>
    /// Actualiza la información de un Paciente
    /// </summary>
    /// <param name="fhirClient"></param>
    /// <param name="patient"></param>
    private async Task<Patient?> UpdatePatientAsync(FhirClient fhirClient, Patient patient)
    {
        patient.Telecom.Add(new ContactPoint()
        {
            System = ContactPoint.ContactPointSystem.Phone,
            Value = txtTelefono.Text,
            Use = ContactPoint.ContactPointUse.Home,
        });
        patient.Gender = AdministrativeGender.Unknown;

        return await fhirClient.UpdateAsync<Patient>(patient);
    }

    private async void btnActualizar_Click(object sender, EventArgs e)
    {
        try
        {
            await UpdatePatientAsync(fhirClient, await ReadPatientAsync(fhirClient, txtId.Text));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
        LimpiarText();
    }
}
