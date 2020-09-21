using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.Threading;
using ZedGraph;
using System.Globalization;

namespace PZ_Reader
{
    public partial class Form1 : Form
    {
        string portName = "COM4";
        public Form1()
        {
            InitializeComponent();
            string[] ports = SerialPort.GetPortNames();
            comboBox1.Items.Clear();
            comboBox1.Items.AddRange(ports);
            _data = new List<double>();
            ZedGraff();

        }

        #region Подключение ПЗ
        private void button1_Click(object sender, EventArgs e)
        {
            portName = comboBox1.SelectedItem.ToString();
            portOpen();
            ReadSetupDevice();
        }
        #endregion

        #region Открытие порта
        SerialPort pzport = new SerialPort();
        private void portOpen()
        {
            pzport.BaudRate = 9600;
            pzport.DataBits = 8;
            pzport.StopBits = StopBits.One;
            pzport.ReadTimeout = 1000;
            pzport.PortName = portName;
            pzport.Open();

            pzport.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);
        }
        #endregion

        #region Запись в порт
        private void portWrite(byte[] command)
        {
            if (!pzport.IsOpen)
                portOpen();
            pzport.Write(command, 0, command.Length);
        }
        #endregion

        #region Чтение порта
        #region событие прихода данных в порт
        private void port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            PortRead();
            if (StaticParametrClass.LastRead)
            {
                byte[] RCommand = new byte[] { 0x52, 0x00, 0x00, 0x00, 0x52, 0x00 };
                portWrite(RCommand);
                StaticParametrClass.LastRead = false;
                StaticParametrClass.CommandJob = false;
                //PortRead();
            }
        }
        #endregion



        #region Чтение порта непосредственно
        //Количество байтов для чтения
        public static int BytesToReadCommand;
        //Сообщение которое пришло в порт
        byte[] ValuePortRead;
        //Время ожидания для записи пакета в порт включая полное ожидание покета
        int SleepTime = 0;
        private void PortRead()
        {
            bool fall = true;
            ValuePortRead = new byte[BytesToReadCommand];
            Thread.Sleep(SleepTime);
            pzport.Read(ValuePortRead, 0, BytesToReadCommand);
            //Проверяем на наличие пакета
            //После изменения установок прибора
                byte package = 88;
                if (ValuePortRead[0] == package)
                {
                    fall = false;
                    StaticParametrClass.WriteNewParametrOk = false;
                }
            //Если флаг пропуска пакета не стоит в отрицательном положении то читаем как положенно
            if (fall)
            {
                ControllSummControl();
            }
        }

        private void ControllSummControl()
        {
            byte[] package = new byte[ValuePortRead.Length - 2];
            byte[] Crc = new byte[2];

            Array.Copy(ValuePortRead, 0, package, 0, ValuePortRead.Length - 2);
            Array.Copy(ValuePortRead, ValuePortRead.Length - 2, Crc, 0, 2);

            byte[] CrcPackage = BitConverter.GetBytes(crc(package));

            if (CrcPackage[0] == Crc[0] && CrcPackage[1] == Crc[1])
            {
                CountPackage += ValuePortRead.Length;
                PortReadCommandSwitch();
            }
            else
            {
                FeilPackage+= ValuePortRead.Length;
            }
            PCDelegate = new PackageCalkulationDelegate(PackageCalculation);
            Invoke(PCDelegate);
        }

        #region Счетчик пакетов
        public delegate void PackageCalkulationDelegate();
        PackageCalkulationDelegate PCDelegate;
        int FeilPackage;
        int CountPackage;

        private void PackageCalculation()
        {
            label7.Text = Convert.ToString(CountPackage);
            label6.Text = Convert.ToString(FeilPackage);
        }
        #endregion

        private void PortReadCommandSwitch()
        {

                switch (StaticParametrClass.CommandName)
                {
                    case "L":
                        StaticParametrClass.CommandJob = false;
                        PackageSetupDevice(ValuePortRead);
                        break;
                    case "E":
                        PackageTensionDevice(ValuePortRead);
                        break;
                    case "W":
                        PackageEnergyFluxDensity(ValuePortRead);
                        break;
                    case "P":
                        PackageExposureElectricField(ValuePortRead);
                        break;
                    case "Q":
                        PackageExposureDensityFlowEnergy(ValuePortRead);
                        break;
                }
            
            
        }
        #endregion
        #endregion

        #region Чтение установок прибора
        private void ReadSetupDevice()
        {
            BytesToReadCommand = 68;
            SleepTime = BytesToReadCommand * 12;
            
            #region Сервисные функции , работа команды и название рабочей команды
            StaticParametrClass.CommandJob = true;
            StaticParametrClass.CommandName = "L";
            #endregion

            byte[] I = new byte[] { 0x4C, 0x00, 0x00, 0x00, 0x4C, 0x00 };
            portWrite(I);
        }

        //Симещение поля
        int[] PackageSetupDiviceShift = new int[] { 0, 2, 3, 6, 9, 12, 15, 18, 21, 24, 27, 45, 53, 66 };
        //Размер поля байтов
        int[] PackageSetupDiviceSize = new int[] { 2, 1, 3, 3, 3, 3, 3, 3, 3, 3, 18, 8, 13, 2 };
        //Массив байтов данных
        object[] PackageSetupDiviceParse = new object[14];
        //Парсинг пакета с учетом размера поля и смещения
        private void PackageSetupDevice(byte[] value)
        {

            string DataString = String.Join(", ", value);

            for (int i = 0; i < PackageSetupDiviceParse.Length; i++)
            {
                int size = PackageSetupDiviceSize[i];
                byte[] VALUE = new byte[size];
                for (int a = 0; a < VALUE.Length; a++)
                {
                    VALUE[a] = value[PackageSetupDiviceShift[i] + a];
                }
                PackageSetupDiviceParse[i] = VALUE;
            }
            TranslitePackageSetupDevice(PackageSetupDiviceParse);
        }

        //Массив множителей
        double[] MultiplerPackageSetupDevice = new double[] { 1, 1, 100.00, 100.00, 100.00, 10000.00, 10.00, 100.00, 100.00, 100.00 };
        //Массив переведенных значений
        object[] PackageSetupDiviceValue = new object[12];

        //Парсинг пакета на значения выдаваемые прибором
        private void TranslitePackageSetupDevice(object[] value)
        {
            //Флаг авторизации измерителя
            StaticParametrClass.AutorisationPZ = true;

            for (int i = 0; i < MultiplerPackageSetupDevice.Length; i++)
            {
                byte[] mask = new byte[] { 0, 0, 0, 0 };
                byte[] objByte = (byte[])value[i];

                #region Вырываем отдельно из пакета авторизации значение поправочного коэфициента
                if (i == 4)
                {
                    StaticParametrClass.PoprKoeficient = objByte;
                }
                #endregion
                Array.Copy(objByte, mask, objByte.Length);
                int val = BitConverter.ToInt32(mask, 0);
                double vald = val / MultiplerPackageSetupDevice[i];
                PackageSetupDiviceValue[i] = vald;
            }
            string ProgrammVersion1 = System.Text.Encoding.ASCII.GetString((byte[])value[12]).Trim('\0');
            string ProgrammVersion = System.Text.Encoding.ASCII.GetString((byte[])value[11]);
            PackageSetupDiviceValue[10] = ProgrammVersion1;
            PackageSetupDiviceValue[11] = ProgrammVersion;

            AddLabel(PackageSetupDiviceValue);
        }

        //int[] PackageSetupDiviceSize = new int[] { 2, 1, 3, 3, 3, 3, 3, 3, 3, 3, 18, 8, 13, 2 };
        string[] LabelDesription = new string[] {"Количество байт в пакете",
        "Индекс номера антенны",
        "Рабочая частота",
        "Поправочный коэфициент",
        "Максимальнео-допустимое значение напряженности электрического поля",
        "Максимально-допустимое значение напряженности магнитного поля",
        "Максимольно-допустимое значение плотности потока электромагнитного поля",
        "Максимально-допустимое значение экспозиции электрического поля",
        "Максимально-допустимое значение экспозиции магнитного поля",
        "Максимально допустимое значение экспозиции потока энергии электромагнитного поля",
        "Параметр прибора",
        "Параметр прибора"};

        string[] LabelName = new string[] { "Кол-во байт: ", "Антенна: ", "Частота: ", "Коэф: ", "мдзнЭп: ", "мдзнМп: ", "мдзппЭМп: ", "мдзЭЭп: ", "мдзЭМп: ", "мдзЭПЭЭп: ", "V. ", "SN: " };

        System.Windows.Forms.Label[] LabelNameValue = new System.Windows.Forms.Label[12];
        System.Windows.Forms.Label[] LabelValue = new System.Windows.Forms.Label[12];

        int startLabelPoint;
        public delegate void GRBDELEGATE(System.Windows.Forms.Label labelnamevalue, System.Windows.Forms.Label labelvalue);
        GRBDELEGATE grbdelegate;

        public delegate ToolTip TOOLTIPDELEGATE(ToolTip tooltip, System.Windows.Forms.Label LabelNameValue, string LabelDesription);
        TOOLTIPDELEGATE tooltipdelegate;

        public delegate System.Windows.Forms.Label LABELVALUEDELEGATE(System.Windows.Forms.Label LabelValue, string value, string LabelName);
        LABELVALUEDELEGATE labelvaluedelegate;

        private void AddLabel(object[] value)
        {
            ToolTip[] tooltip = new ToolTip[12];

            grbdelegate = new GRBDELEGATE(AddLabelFromDelegate);

            tooltipdelegate = new TOOLTIPDELEGATE(ToolTipsAdd);

            labelvaluedelegate = new LABELVALUEDELEGATE(LabelValueDelegate);

            startLabelPoint = 22;
            for (int i = 0; i < LabelNameValue.Length; i++)
            {
                if(LabelNameValue[i] == null)
                LabelNameValue[i] = new System.Windows.Forms.Label();
                LabelNameValue[i].Text = LabelName[i];

                StaticParametrClass.PriborParametrSetups[i] = value[i];
                LabelValue[i] = (System.Windows.Forms.Label)Invoke(labelvaluedelegate, LabelValue[i], Convert.ToString(value[i]), Convert.ToString(i));
                tooltip[i] = (ToolTip)Invoke(tooltipdelegate, tooltip[i], LabelNameValue[i], LabelDesription[i]);

                if (i == 5)
                {
                    int o = 0;
                }
                
                if (i != 0 && i != 10 && i != 11)
                {
                    LabelNameValue[i].Location = new Point(6, startLabelPoint);
                    LabelValue[i].Location = new Point(122, startLabelPoint);
                    startLabelPoint = startLabelPoint + 29;
                    Invoke(grbdelegate, LabelNameValue[i], LabelValue[i]);
                }
            }
        }

        private System.Windows.Forms.Label LabelValueDelegate(System.Windows.Forms.Label LabelValue, string value, string LabelName)
        {
            if (LabelValue == null)
            {
                LabelValue = new System.Windows.Forms.Label();
                LabelValue.Name = LabelName;
                LabelValue.DoubleClick += LabelValue_DoubleClick;
            }
            LabelValue.Text = Convert.ToString(value);
            if (!LabelValue.Visible)
            {
                int LabelValueName = Convert.ToInt32(LabelValue.Name);
                groupBox2.Controls.Remove(NewParametrTextBox[LabelValueName]);
                LabelValue.Visible = true;

            }
            return LabelValue;
        }
        

        private ToolTip ToolTipsAdd(ToolTip tooltip, System.Windows.Forms.Label LabelNameValue, string LabelDesription)
        {
            if (tooltip == null)
                tooltip = new ToolTip();
            tooltip.SetToolTip(LabelNameValue, LabelDesription);

            return tooltip;
        }

        TextBox[] NewParametrTextBox = new TextBox[12];
        bool[] UbdateParametrTextBox = new bool[12];
             
        private void LabelValue_DoubleClick(object sender, EventArgs e)
        {
            System.Windows.Forms.Label LabelEvent = (System.Windows.Forms.Label)sender;
            int LbName = Convert.ToInt32(LabelEvent.Name);
            if (NewParametrTextBox[LbName] == null)
            {
                NewParametrTextBox[LbName] = new TextBox();
                NewParametrTextBox[LbName].Name = LabelEvent.Name;
            }

            NewParametrTextBox[LbName].Text = LabelEvent.Text;
            UbdateParametrTextBox[LbName] = true;
            NewParametrTextBox[LbName].Location = new Point(LabelEvent.Location.X, LabelEvent.Location.Y);
            LabelEvent.Visible = false;
            groupBox2.Controls.Add(NewParametrTextBox[LbName]);
        }

        private void AddLabelFromDelegate(System.Windows.Forms.Label labelnamevalue, System.Windows.Forms.Label labelvalue)
        {
            groupBox2.Controls.Add(labelnamevalue);
            groupBox2.Controls.Add(labelvalue);
        }

        #endregion

        #region ZedGraph

        //Максимальный размер очереди
        int _capasity = 100;

        //Здесь храним данные
        List<double> _data;

        //Интервал изменения данных по вертикали
        double _ymin = 0;
        double _ymax = 10;

        private void ZedGraff()
        {
            //Получим панель для рисования
            GraphPane pane = zedGraphControl1.GraphPane;
            //Очистка пано от графика
            pane.CurveList.Clear();

            //Создаем список точек
            PointPairList list = new PointPairList();

            //Интервал где есть данные
            double _xmin = 0;
            double _xmax = _capasity;

            //Расстояние между соседними точками по горизонтали
            double dx = 1.0;
            double curr_x = 0;

            // Заполняем список точек
            if(_data != null)
            foreach (double val in _data)
            {
                list.Add(curr_x, val);
                curr_x += dx;
            }

            

            pane.CurveList.Clear();
            LineItem myCurve = pane.AddCurve("Random Value", list, Color.Blue, SymbolType.Circle);
            myCurve.Line.Width = 2f;
            //Заливка под кривой графика
            //myCurve.Line.Fill = new ZedGraph.Fill(Color.Red, Color.Yellow, Color.Blue, 90.0f);
            // myCurve.Line.Fill = new ZedGraph.Fill(Color.Green);

            // Включим сглаживание
            //myCurve.Line.IsSmooth = true;

            #region Убрать некоторые элементы ЗГ
            //Убрать легенду
            myCurve.Label.IsVisible = false;
            
            // Титул, и название осей
            pane.Title.IsVisible = false;

            pane.XAxis.Title.IsVisible = false;

            pane.YAxis.Title.IsVisible = false;
            #endregion

            //Устанавливаем интервал по оси Х
            pane.XAxis.Scale.Min = _xmin;
            pane.XAxis.Scale.Max = _xmax;

            //Устанавливаем интервал по оси Y
            pane.YAxis.Scale.Min = _ymin;

            if (_data.Sum() == 0)
            {
                pane.YAxis.Scale.Max = _ymax;
            }
            else
            {
                pane.YAxis.Scale.Max = _data.Max() + 1;
            }
            

            //Вызываем метод AxisChange(), что бы обновить данные об осях
            //В противном случае на рисунке будет показанна только часть графика по умолчанию
            // Которая умещается в интервалы по осям установленным по умолчанию

            zedGraphControl1.AxisChange();

            //Обновляем график
            zedGraphControl1.Invalidate();
        }

        #region тестовая кнопка на Q 

        public delegate void fff();
        fff ff;

        private void TensionGraficsUpdate(double newValue)
        {
            ff = new fff(ZedGraff);
            _data.Add(newValue);
            if (_data.Count > _capasity)
            {
                //_capasity++;
                _data.RemoveAt(0);
            }
            Invoke(ff);
        }

        #endregion
        #endregion

        #region Запрос на получение напряженности в рельном времени
        private void button2_Click(object sender, EventArgs e)
        {
            ReadTension();
        }

        private void ReadTension()
        {
            if (StaticParametrClass.AutorisationPZ)
            {
                if (!StaticParametrClass.CommandJob)
                {
                    BytesToReadCommand = 7;
                    SleepTime = BytesToReadCommand * 12;

                    #region Сервисные функции , работа команды и название рабочей команды
                    StaticParametrClass.CommandJob = true;
                    StaticParametrClass.CommandName = "E";
                    #endregion

                    byte[] I = new byte[] { 0x45, 0x00, 0x00, 0x00, 0x45, 0x00 };
                    portWrite(I);
                }
                else
                {
                    //Идет незавершенное выполнение команды
                    //Добавить вывод шибки
                    // Имя команды на выполнении
                    string commandName = StaticParametrClass.CommandName;
                    CommandStopTreadStart("E");
                }
            }
            else
            {
                //Вывод ошибки, авторизация не прошла
                //Прибор не подключен
            }
        }

        //Симещение поля
        int[] PackageTensionDiviceShift = new int[] { 0,2,5 };
        
        //Размер поля байтов
        int[] PackageTensionDiviceSize = new int[] { 2,3,2 };
        
        //Массив байтов данных
        object[] PackageTensionDiviceParse = new object[3];

        //Парсинг пакета с учетом размера поля и смещения
        private void PackageTensionDevice(byte[] value)
        {
            for (int i = 0; i < PackageTensionDiviceParse.Length; i++)
            {
                int size = PackageTensionDiviceSize[i];
                byte[] VALUE = new byte[size];
                for (int a = 0; a < VALUE.Length; a++)
                {
                    VALUE[a] = value[PackageTensionDiviceShift[i] + a];
                }
                PackageTensionDiviceParse[i] = VALUE;
            }
            TranslitePackageTensionDevice(PackageTensionDiviceParse);
        }

        //Массив множителей
        int[] MultiplerPackageTensionDevice = new int[] { 1, 10000, 1 };
        //Массив переведенных значений
        object[] PackageTensionDiviceValue = new object[3];

        //Парсинг пакета на значения выдаваемые прибором
        private void TranslitePackageTensionDevice(object[] value)
        {
            ultdelegate = new ULTDelegate(UpdateLabelTension);
            for (int i = 0; i < MultiplerPackageTensionDevice.Length; i++)
            {
                double vald;
                if (i == 1)
                {
                    UInt32 res = 0;
                    int j = 0;
                    foreach (byte ms in (byte[])value[i])
                    {
                        res += Convert.ToUInt32(ms) << j;
                        j += 8;
                    }
                    vald = (double)res / 10000.00;
                }
                else
                {
                    byte[] mask = new byte[] { 0, 0, 0, 0 };
                    byte[] objByte = (byte[])value[i];
                    Array.Copy(objByte, mask, objByte.Length);
                    int val = BitConverter.ToInt32(mask, 0);
                    vald = val / MultiplerPackageTensionDevice[i];
                    
                }
                PackageTensionDiviceValue[i] = vald;
                
            }
            Invoke(ultdelegate, (double)PackageTensionDiviceValue[1]);
            TensionGraficsUpdate((double)PackageTensionDiviceValue[1]);
        }

        #endregion

        #region Получить установки прибора КНОПКА "Обновить"
        private void button4_Click(object sender, EventArgs e)
        {
            ReadSetupDevice();
        }
        #endregion

        #region Записать новые значения в Измеритель кнопка "Записать"
        private void button5_Click(object sender, EventArgs e)
        {
            StartUpdateParametrThread();
        }

        Thread UpdateParametrThread;
        private void StartUpdateParametrThread()
        {
            UpdateParametrThread = new Thread(ParametrLabelGet);
            UpdateParametrThread.Start();
        }

        private void ParametrLabelGet()
        {
            //Обновление информации о параметрах
            UpdateParametrStaticClass();
            PackageUpdateMesuremetnter();

        }

        private void UpdateParametrStaticClass()
        {
            #region сепаратор для разделения строки по значению double
            NumberFormatInfo provider = new NumberFormatInfo();
            provider.NumberGroupSeparator = ",";
            #endregion

            for (int i =0;i< UbdateParametrTextBox.Length;i++)
            { 
                if (UbdateParametrTextBox[i] == true)
                {
                    string parametrTb = NewParametrTextBox[i].Text;
                    double doubleNumber = Convert.ToDouble(parametrTb);
                    StaticParametrClass.PriborParametrSetups[i] = doubleNumber;
                }
            }
        }

        private void PackageUpdateMesuremetnter()
        {
            //double Fq = (double)StaticParametrClass.PriborParametrSetups[2];
            //double MaxE = (double)StaticParametrClass.PriborParametrSetups[4];
            //double MaxH = (double)StaticParametrClass.PriborParametrSetups[5];
            //double PPE = (double)StaticParametrClass.PriborParametrSetups[6];
            //double ExpE = (double)StaticParametrClass.PriborParametrSetups[7];
            //double ExpH = (double)StaticParametrClass.PriborParametrSetups[8];
            //double ExpPPE = (double)StaticParametrClass.PriborParametrSetups[9];
            int[] ParametrUpdateIndex = new int[] { 2, 4, 5, 6, 7, 8, 9};
            int[] ParametrPositionIndex = new int[] {5, 11,14,17,20,23,26};

            byte[] PackageParametrUpdate = new byte[] { 75, 0, 25, 0, 3,
                88, 2, 0,
                126,37, 0,
                188, 138, 169,
                53,66, 15,
                52, 115, 203,
                112, 103, 220,
                16, 92, 237, 76, 80, 254 };

            for (int i = 0; i< ParametrUpdateIndex.Length; i++)
            {
                int parametr = Convert.ToInt32((double)StaticParametrClass.PriborParametrSetups[ParametrUpdateIndex[i]] * MultiplerPackageSetupDevice[ParametrUpdateIndex[i]]);
                byte[] parametrByte = BitConverter.GetBytes(parametr);
                Array.Copy(parametrByte, 0, PackageParametrUpdate, ParametrPositionIndex[i], 3);

            }
            
            //Вставляем в отправочный пакет значение поправочного коэфициента
            Array.Copy(StaticParametrClass.PoprKoeficient, 0, PackageParametrUpdate, 8, 3);

            byte[] Antenna = BitConverter.GetBytes(Convert.ToInt32((double)StaticParametrClass.PriborParametrSetups[1]));
            PackageParametrUpdate[4] = Antenna[0];

            //
            string DataString = String.Join(", ", PackageParametrUpdate);

            var ControllSumm = BitConverter.GetBytes(crc(PackageParametrUpdate));

            Array.Resize(ref PackageParametrUpdate, PackageParametrUpdate.Length + 2);

            Array.Copy(ControllSumm,0, PackageParametrUpdate,29,2);

            //Запись в прибор строки с обновленной информацией
           ControllCommands(PackageParametrUpdate);

            ////Синхронизация прибора после обновления
            //ReadSetupDevice();

        }

        private ushort crc(byte[] data, ushort sum = 0)
        {
            foreach (byte b in data) sum += b;
            return sum;
        }

        #endregion

        #region Проверка занят ли порт как койф либо командой
        private void ControllCommands(byte[] command)
        {
            radDelegate = new ReadSetupDeviceDelegate(ReadSetupDevice);
            if (StaticParametrClass.CommandJob)
            {
                StaticParametrClass.LastRead = true;
                string CommandRead = StaticParametrClass.CommandName;
                while (StaticParametrClass.LastRead)
                {
                    Thread.Sleep(100);
                }
                StaticParametrClass.WriteNewParametrOk = true;
                portWrite(command);
                StaticParametrClass.AutorisationPZ = false;
                while (StaticParametrClass.WriteNewParametrOk)
                {
                    Thread.Sleep(100);
                }
                Invoke(radDelegate);
                while (!StaticParametrClass.AutorisationPZ)
                {
                    Thread.Sleep(100);
                }
                switch (CommandRead)
                {
                    case "L":
                        break;
                    case "E":
                        ReadTension();
                        break;
                    case "W":
                        ReadEnergyFluxDensity();
                        break;
                    case "P":
                        ReadExposureElectricField();
                        break;
                    case "Q":
                        ReadExposureDensityFlowEnergy();
                        break;

                }
            }
            else
            {
                StaticParametrClass.WriteNewParametrOk = true;
                portWrite(command);
                StaticParametrClass.AutorisationPZ = false;
                //ReadSetupDevice();
                while (StaticParametrClass.WriteNewParametrOk)
                {
                    Thread.Sleep(100);
                }
                Invoke(radDelegate);
            }
        }

        public delegate void ReadSetupDeviceDelegate();
        ReadSetupDeviceDelegate radDelegate;
        #endregion

        #region Обновление динамических значений напряженности на форме
        public delegate void ULTDelegate(double value);
        ULTDelegate ultdelegate;
        private void UpdateLabelTension(double value)
        {
            label1.Text = Convert.ToString(Math.Round(value, 3));

            if (_data.Sum() == 0)
            {
                label2.Text = Convert.ToString(0);
                label5.Text = Convert.ToString(0);
            }
            else
            {
                label2.Text = Convert.ToString(Math.Round(_data.Max(), 3));
                label5.Text = Convert.ToString(Math.Round(_data.Min(), 3));
            }
            


        }
        #endregion
        private void Form1_Load(object sender, EventArgs e)
        {
            
        }

        #region Принудительная остановка выполнения команды передачис приемника информации
        private void button8_Click(object sender, EventArgs e)
        {
            byte[] RCommand = new byte[] { 0x52, 0x00, 0x00, 0x00, 0x52, 0x00 };
            portWrite(RCommand);
        }
        #endregion

        #region Остановка выполнения установленной команды и начало выполнения другой команды
        Thread CommandStopThread;

        private void CommandStopTreadStart(string CommandName)
        {
            StaticParametrClass.NewCommand = CommandName;
            CommandStopThread = new Thread(CommandStop);
            CommandStopThread.Start();
        }
        private void CommandStop(object CommandName)
        {
            StaticParametrClass.LastRead = true;
            while (StaticParametrClass.LastRead)
            {
                Thread.Sleep(100);
            }

            switch (StaticParametrClass.NewCommand)
            {
                case "L":
                    ReadSetupDevice();
                    break;
                case "E":
                    ReadTension();
                    break;
                case "W":
                    ReadEnergyFluxDensity();
                    break;
                case "P":
                    ReadExposureElectricField();
                    break;
                case "Q":
                    ReadExposureDensityFlowEnergy();
                    break;
            }

        }
        #endregion

        #region Команда W - вывод текущих значений плотности потока энергии (ППЭ)
        private void button3_Click(object sender, EventArgs e)
        {
            ReadEnergyFluxDensity();
        }

        private void ReadEnergyFluxDensity()
        {
            if (StaticParametrClass.AutorisationPZ)
            {
                if (!StaticParametrClass.CommandJob)
                {
                    BytesToReadCommand = 10;
                    SleepTime = BytesToReadCommand * 12;

                    #region Сервисные функции , работа команды и название рабочей команды
                    StaticParametrClass.CommandJob = true;
                    StaticParametrClass.CommandName = "W";
                    #endregion

                    byte[] I = new byte[] { 0x57, 0x00, 0x00, 0x00, 0x57, 0x00 };
                    portWrite(I);
                }
                else
                {
                    //Идет незавершенное выполнение команды
                    //Добавить вывод шибки
                    // Имя команды на выполнении
                    string commandName = StaticParametrClass.CommandName;
                    CommandStopTreadStart("W");
                }
            }
            else
            {
                //Вывод ошибки, авторизация не прошла
                //Прибор не подключен
            }
        }

        //Симещение поля
        int[] PackageEnergyFluxDensityShift = new int[] { 0, 2, 8 };

        //Размер поля байтов
        int[] PackageEnergyFluxDensitySize = new int[] { 2, 6, 2 };

        //Массив байтов данных
        object[] PackageEnergyFluxDensityParse = new object[3];

        //Парсинг пакета с учетом размера поля и смещения
        private void PackageEnergyFluxDensity(byte[] value)
        {
            for (int i = 0; i < PackageEnergyFluxDensityParse.Length; i++)
            {
                int size = PackageEnergyFluxDensitySize[i];
                byte[] VALUE = new byte[size];
                for (int a = 0; a < VALUE.Length; a++)
                {
                    VALUE[a] = value[PackageEnergyFluxDensityShift[i] + a];
                }
                PackageEnergyFluxDensityParse[i] = VALUE;
            }
            TransliteEnergyFluxDensityDevice(PackageEnergyFluxDensityParse);
        }

        //Массив множителей
        double[] MultiplerPackageEnergyFluxDensity = new double[] { 1.0, 100000.00, 1.0 };
        //Массив переведенных значений
        object[] PackageEnergyFluxDensityValue = new object[3];

        //Парсинг пакета на значения выдаваемые прибором
        private void TransliteEnergyFluxDensityDevice(object[] value)
        {
            ultdelegate = new ULTDelegate(UpdateLabelTension);
            for (int i = 0; i < MultiplerPackageEnergyFluxDensity.Length; i++)
            {
                double vald;
                if (i == 1)
                {
                    UInt32 res = 0;
                    int j = 0;
                    foreach (byte ms in (byte[])value[i])
                    {
                        res += Convert.ToUInt32(ms) << j;
                        j += 8;
                    }
                    vald = (double)res / MultiplerPackageEnergyFluxDensity[i];
                }
                else
                {
                    byte[] mask = new byte[] { 0, 0, 0, 0 };
                    byte[] objByte = (byte[])value[i];
                    Array.Copy(objByte, mask, objByte.Length);
                    int val = BitConverter.ToInt32(mask, 0);
                    vald = val / MultiplerPackageEnergyFluxDensity[i];

                }
                PackageEnergyFluxDensityValue[i] = vald;

            }
            Invoke(ultdelegate, (double)PackageEnergyFluxDensityValue[1]);
            TensionGraficsUpdate((double)PackageEnergyFluxDensityValue[1]);
        }
        #endregion

        #region Команда P - вывод экеспозиции электрического поля (ЭЭП)
        private void button6_Click(object sender, EventArgs e)
        {
            ReadExposureElectricField();
        }

        private void ReadExposureElectricField()
        {
            if (StaticParametrClass.AutorisationPZ)
            {
                if (!StaticParametrClass.CommandJob)
                {
                    BytesToReadCommand = 10;
                    SleepTime = BytesToReadCommand * 12;

                    #region Сервисные функции , работа команды и название рабочей команды
                    StaticParametrClass.CommandJob = true;
                    StaticParametrClass.CommandName = "P";
                    #endregion

                    byte[] I = new byte[] { 0x50, 0x00, 0x00, 0x00, 0x50, 0x00 };
                    portWrite(I);
                }
                else
                {
                    //Идет незавершенное выполнение команды
                    //Добавить вывод шибки
                    // Имя команды на выполнении
                    string commandName = StaticParametrClass.CommandName;
                    CommandStopTreadStart("P");
                }
            }
            else
            {
                //Вывод ошибки, авторизация не прошла
                //Прибор не подключен
            }
        }

        //Симещение поля
        int[] PackageExposureElectricFieldShift = new int[] { 0, 2, 8 };

        //Размер поля байтов
        int[] PackageExposureElectricFieldSize = new int[] { 2, 6, 2 };

        //Массив байтов данных
        object[] PackageExposureElectricFieldParse = new object[3];

        //Парсинг пакета с учетом размера поля и смещения
        private void PackageExposureElectricField(byte[] value)
        {
            for (int i = 0; i < PackageExposureElectricFieldParse.Length; i++)
            {
                int size = PackageExposureElectricFieldSize[i];
                byte[] VALUE = new byte[size];
                for (int a = 0; a < VALUE.Length; a++)
                {
                    VALUE[a] = value[PackageExposureElectricFieldShift[i] + a];
                }
                PackageExposureElectricFieldParse[i] = VALUE;
            }
            TransliteExposureElectricFieldDevice(PackageExposureElectricFieldParse);
        }

        //Массив множителей
        double[] MultiplerPackageExposureElectricField = new double[] { 1.0, 1000.00, 1.0 };
        //Массив переведенных значений
        object[] PackageExposureElectricFieldValue = new object[3];

        //Парсинг пакета на значения выдаваемые прибором
        private void TransliteExposureElectricFieldDevice(object[] value)
        {
            ultdelegate = new ULTDelegate(UpdateLabelTension);
            for (int i = 0; i < MultiplerPackageExposureElectricField.Length; i++)
            {
                double vald;
                if (i == 1)
                {
                    UInt32 res = 0;
                    int j = 0;
                    foreach (byte ms in (byte[])value[i])
                    {
                        res += Convert.ToUInt32(ms) << j;
                        j += 8;
                    }
                    vald = (double)res / MultiplerPackageExposureElectricField[i];
                }
                else
                {
                    byte[] mask = new byte[] { 0, 0, 0, 0 };
                    byte[] objByte = (byte[])value[i];
                    Array.Copy(objByte, mask, objByte.Length);
                    int val = BitConverter.ToInt32(mask, 0);
                    vald = val / MultiplerPackageExposureElectricField[i];

                }
                PackageExposureElectricFieldValue[i] = vald;

            }
            Invoke(ultdelegate, (double)PackageExposureElectricFieldValue[1]);
            TensionGraficsUpdate((double)PackageExposureElectricFieldValue[1]);
        }

        #endregion

        #region Команда Q - Вывод экспозиции ППЭ
        private void button7_Click(object sender, EventArgs e)
        {
            ReadExposureDensityFlowEnergy();
        }

        private void ReadExposureDensityFlowEnergy()
        {
            if (StaticParametrClass.AutorisationPZ)
            {
                if (!StaticParametrClass.CommandJob)
                {
                    BytesToReadCommand = 10;
                    SleepTime = BytesToReadCommand * 12;

                    #region Сервисные функции , работа команды и название рабочей команды
                    StaticParametrClass.CommandJob = true;
                    StaticParametrClass.CommandName = "Q";
                    #endregion

                    byte[] I = new byte[] { 0x51, 0x00, 0x00, 0x00, 0x51, 0x00 };
                    portWrite(I);
                }
                else
                {
                    //Идет незавершенное выполнение команды
                    //Добавить вывод шибки
                    // Имя команды на выполнении
                    string commandName = StaticParametrClass.CommandName;
                    CommandStopTreadStart("Q");
                }
            }
            else
            {
                //Вывод ошибки, авторизация не прошла
                //Прибор не подключен
            }
        }

        //Симещение поля
        int[] PackageExposureDensityFlowEnergyShift = new int[] { 0, 2, 8 };

        //Размер поля байтов
        int[] PackageExposureDensityFlowEnergySize = new int[] { 2, 6, 2 };

        //Массив байтов данных
        object[] PackageExposureDensityFlowEnergyParse = new object[3];

        //Парсинг пакета с учетом размера поля и смещения
        private void PackageExposureDensityFlowEnergy(byte[] value)
        {
            for (int i = 0; i < PackageExposureDensityFlowEnergyParse.Length; i++)
            {
                int size = PackageExposureDensityFlowEnergySize[i];
                byte[] VALUE = new byte[size];
                for (int a = 0; a < VALUE.Length; a++)
                {
                    VALUE[a] = value[PackageExposureDensityFlowEnergyShift[i] + a];
                }
                PackageExposureDensityFlowEnergyParse[i] = VALUE;
            }
            TransliteExposureDensityFlowEnergyDevice(PackageExposureDensityFlowEnergyParse);
        }

        //Массив множителей
        double[] MultiplerPackageExposureDensityFlowEnergy = new double[] { 1.0, 10000.00, 1.0 };
        //Массив переведенных значений
        object[] PackageExposureDensityFlowEnergyValue = new object[3];

        //Парсинг пакета на значения выдаваемые прибором
        private void TransliteExposureDensityFlowEnergyDevice(object[] value)
        {
            ultdelegate = new ULTDelegate(UpdateLabelTension);
            for (int i = 0; i < MultiplerPackageExposureDensityFlowEnergy.Length; i++)
            {
                double vald;
                if (i == 1)
                {
                    UInt32 res = 0;
                    int j = 0;
                    foreach (byte ms in (byte[])value[i])
                    {
                        res += Convert.ToUInt32(ms) << j;
                        j += 8;
                    }
                    vald = (double)res / MultiplerPackageExposureDensityFlowEnergy[i];
                }
                else
                {
                    byte[] mask = new byte[] { 0, 0, 0, 0 };
                    byte[] objByte = (byte[])value[i];
                    Array.Copy(objByte, mask, objByte.Length);
                    int val = BitConverter.ToInt32(mask, 0);
                    vald = val / MultiplerPackageExposureDensityFlowEnergy[i];

                }
                PackageExposureDensityFlowEnergyValue[i] = vald;

            }
            Invoke(ultdelegate, (double)PackageExposureDensityFlowEnergyValue[1]);
            TensionGraficsUpdate((double)PackageExposureDensityFlowEnergyValue[1]);
        }
        #endregion

        private void label3_Click(object sender, EventArgs e)
        {

        }
    }
}
