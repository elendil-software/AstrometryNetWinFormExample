using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using software.elendil.AstrometryNet;
using software.elendil.AstrometryNet.Enum;
using software.elendil.AstrometryNet.Json;

namespace TestWinForms
{

    public struct Solution
    {
        public double RA;
        public double Dec;
        public double Radius;
        public SolverStatus Status;
    }

    public enum SolverStatus
    {
        Connected,
        NotConnected,
        ImageSent,
        Solving,
        Success,
        Failure,
        Error,
        Canceled
    }

    public partial class Form1 : Form
    {
        private Client client;
        private string submissionId;
        private SolverStatus status;
        private string Message { get; set; }

        private CancellationTokenSource cancellationTokenSource;


        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            cancellationTokenSource = new CancellationTokenSource();
            status = SolverStatus.NotConnected;
            Solve();
        }

        private async void Solve()
        {
            bool imageSent = await SendImage(textBoxFile.Text, cancellationTokenSource.Token);
			if (!imageSent)
			{
                textBoxLog.Text += Message + Environment.NewLine;
			}
			else
			{
				cancellationTokenSource.Token.ThrowIfCancellationRequested();
                textBoxLog.Text += @"Solving..." + Environment.NewLine;
				Solution res = await GetSolution(cancellationTokenSource.Token);

			    if (SolverStatus.Success.Equals(res.Status))
			    {
			        textBoxLog.Text += $@"Solution : RA : {res.RA} , Dec : {res.Dec} : {Environment.NewLine}";
			    }
			    else
			    {
			        textBoxLog.Text += Message + Environment.NewLine;
			    }
			}
        }

        private void Login()
        {
            if (status == SolverStatus.NotConnected)
            {
                client = new Client(textBoxApiKey.Text);
                LoginResponse result = client.Login();

                if (ResponseStatus.success.Equals(result.status))
                {
                    Message = result.message;
                    status = SolverStatus.Connected;
                }
                else
                {
                    Message = result.errormessage;
                    status = SolverStatus.NotConnected;
                }
            }
            else
            {
                status = SolverStatus.Connected;
            }
        }

        public async Task<bool> SendImage(string file, CancellationToken ct)
        {
            return await Task.Factory.StartNew(() =>
            {
                Login();

                if (status != SolverStatus.Connected)
                {
                    return false;
                }

                submissionId = null;

                UploadResponse result = client.Upload(file);

                if (ResponseStatus.success.Equals(result.status))
                {
                    submissionId = result.subid;
                    Message = file;
                    status = SolverStatus.ImageSent;
                    return true;
                }

                Message = result.errormessage;
                return false;
            }, ct);
        }

        /// <summary>
		/// Get the solution (if solved)
		/// </summary>
		/// <returns></returns>
		public async Task<Solution> GetSolution(CancellationToken ct)
        {
            var solution = new Solution();
            var jobStatusResponse = new JobStatusResponse { status = ResponseJobStatus.failure };

            var submissionStatusResponse = await client.GetSubmissionStatus(submissionId, ct);

            if (submissionStatusResponse.jobs != null && !ct.IsCancellationRequested)
            {
                jobStatusResponse = await client.GetJobStatus(submissionStatusResponse.jobs[0], ct);
            }


            if (submissionStatusResponse.jobs != null && jobStatusResponse.status.Equals(ResponseJobStatus.success) &&
                !ct.IsCancellationRequested)
            {
                CalibrationResponse calibrationResponse = client.GetCalibration(submissionStatusResponse.jobs[0]);

                solution.RA = calibrationResponse.ra;
                solution.Dec = calibrationResponse.dec;
                solution.Radius = calibrationResponse.radius;
                solution.Status = SolverStatus.Success;
                return solution;
            }

            if (ct.IsCancellationRequested)
            {
                solution.Status = SolverStatus.Canceled;
                return solution;
            }

            solution.Status = SolverStatus.Failure;
            return solution;
        }
    }
}
