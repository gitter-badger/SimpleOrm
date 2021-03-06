﻿using Microsoft.Practices.Prism.Commands;
using Microsoft.Practices.Prism.Mvvm;
using ORM.VSPackage.ImportWindowSqlServer.CustomEventArgs;
using ORM.VSPackage.ImportWindowSqlServer.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ORM.VSPackage.ImportWindowSqlServer.ViewModels
{
    public class ImportViewModel : BindableBase
    {
        private bool _isWindowsAuthenticationEnabled;

        private bool _isConnectionSuccessful;

        public ImportViewModel()
        {
            _isWindowsAuthenticationEnabled = false;
            _isConnectionSuccessful = false;
            Tables = new ObservableCollection<SelectedTable>();

            RegisterCommands();
        }

        public delegate void ImportTablesHandler(object sender, ImportTablesEventArgs args);

        public event ImportTablesHandler ImportTablesEvent;

        public ICommand EnableWindowsAuthenticationCommand { get; private set; }

        public ICommand TestConnectionCommand { get; private set; }

        public ICommand GenerateTablesCommand { get; private set; }

        public string DataSource { private get; set; }

        public string UserName { private get; set; }

        public string Password { private get; set; }

        public string Catalog { private get; set; }

        public bool IsWindowsAuthenticationEnabled
        {
            get
            {
                return _isWindowsAuthenticationEnabled;
            }
            set
            {
                SetProperty(ref _isWindowsAuthenticationEnabled, value);
            }
        }

        public bool IsConnectionSuccessful
        {
            get
            {
                return _isConnectionSuccessful;
            } 
            set
            {
                SetProperty(ref _isConnectionSuccessful, value);
            }
        }

        public ObservableCollection<SelectedTable> Tables { get; set; }

        private void RegisterCommands()
        {
            EnableWindowsAuthenticationCommand = new DelegateCommand(EnableWindowsAuthenticationExecute);
            TestConnectionCommand = new DelegateCommand(TestConnectionExecute);
            GenerateTablesCommand = new DelegateCommand(GenerateTablesCommandExecute);
        }

        private void EnableWindowsAuthenticationExecute()
        {
            IsWindowsAuthenticationEnabled = !IsWindowsAuthenticationEnabled;
        }

        private async void TestConnectionExecute()
        {
            Tables.Clear();
            var connectionString = CreateConnectionString();
            var connectionStringValid = await IsConnectionStringValid(connectionString);
            if (!connectionStringValid) {
                MessageBox.Show("Cannot establish the connection");
                return;
            }

            MessageBox.Show("Connection successful");
            IsConnectionSuccessful = connectionStringValid;
            var tables = await GetTables(connectionString);
            foreach(var table in  tables)
            {
                Tables.Add(table);
            }
        }

        private void GenerateTablesCommandExecute()
        {
            if (ImportTablesEvent != null)
            {
                var connectionString = CreateConnectionString();
                var selectedTables = Tables.Where(t => t.IsSelected).Select(t => t.TableDefinition).ToList();
                var argument = new ImportTablesEventArgs(selectedTables, connectionString);
                ImportTablesEvent(this, argument);
            }
        }

        private string CreateConnectionString()
        {
            var connectionString = string.Format("Data Source={0};Initial Catalog={1};", DataSource, Catalog);
            if (IsWindowsAuthenticationEnabled)
            {
                connectionString += "Integrated Security=True;";
            }
            else
            {
                connectionString += string.Format("User Id={0};Password={1};", UserName, Password);
            }

            return connectionString;
        }

        private async Task<bool> IsConnectionStringValid(string connectionString)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private async Task<List<SelectedTable>> GetTables(string connectionString)
        {
            var result = new List<SelectedTable>();
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var command = new SqlCommand("SELECT TABLE_SCHEMA,TABLE_NAME FROM information_schema.tables", connection);
                var reader = await command.ExecuteReaderAsync();
                while(reader.Read())
                {
                    var record = new SelectedTable
                    {
                        IsSelected = true
                    };

                    var tableDefinition = new TableDefinition
                    {
                        TableSchema = reader.GetString(0),
                        TableName = reader.GetString(1),
                        ColumnDefinitions = new List<ColumnDefinition>()
                    };

                    await GetColumnDefinitions(tableDefinition.TableName, connectionString, tableDefinition.ColumnDefinitions);

                    record.TableDefinition = tableDefinition;
                    result.Add(record);
                }


                reader.Close();
            }

            return result;
        }

        private async Task GetColumnDefinitions(string tableName, string connectionString, List<ColumnDefinition> columnDefinitions)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var tableStructureCommand = new SqlCommand(string.Format("SELECT COLUMN_NAME,DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{0}'", tableName), connection);
                var tableStructureReader = await tableStructureCommand.ExecuteReaderAsync();
                while (tableStructureReader.Read())
                {
                    var columnDefinition = new ColumnDefinition
                    {
                        ColumnName = tableStructureReader.GetString(0),
                        ColumnType = tableStructureReader.GetString(1)
                    };

                    columnDefinitions.Add(columnDefinition);
                }

                tableStructureReader.Close();
                connection.Close();
            }
        }
    }
}
