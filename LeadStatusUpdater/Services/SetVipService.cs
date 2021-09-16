﻿using LeadStatusUpdater.Constants;
using LeadStatusUpdater.Enums;
using LeadStatusUpdater.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LeadStatusUpdater.Services
{
    public class SetVipService : ISetVipService
    {
        private IRequestsSender _requests;
        private const string _dateFormatWithMinutesAndSeconds = "dd.MM.yyyy HH:mm";
        private string _adminToken;


        public SetVipService(IRequestsSender sender
            )
        {
            _requests = sender;

        }

        public void Process()
        {
            _adminToken = _requests.GetAdminToken();

            var leads = new List<LeadOutputModel>();
            var leadsToChangeStatusList = new List<LeadIdAndRoleInputModel>();
            var leadsToLogAndEmail = new List<LeadOutputModel>();
            int lastLeadId = 0;
            int leadsCount = 0;

            do
            {
                leads = _requests.GetRegularAndVipLeads(_adminToken, lastLeadId);
                leadsCount = leads.Count;

                if (leads != null && leadsCount > 0)
                {
                    Log.Information($"{leadsCount} leads were retrieved from database");

                    leads.ForEach(lead =>
                    {
                        var newRole = CheckOneLead(lead) ? Role.Vip : Role.Regular;
                        if (lead.Role != newRole)
                        {
                            leadsToChangeStatusList.Add(new LeadIdAndRoleInputModel { Id = lead.Id, Role = newRole });
                        }
                    });

                    //_requests.ChangeStatus(leadsToChangeStatusList, _adminToken); //change

                    leadsToLogAndEmail.AddRange(leads.Where(l => leadsToChangeStatusList.Any(c => l.Id == c.Id)));

                    lastLeadId = leads.Last().Id;
                }
            }
            while (leads != null && leadsCount > 0);

            Log.Information($"All leads were processed");
            foreach (var lead in leadsToLogAndEmail)
            {
                string logMessage = lead.Role == Role.Vip ? $"{LogMessages.VipStatusGiven} " : $"{LogMessages.VipStatusTaken} ";
                logMessage = string.Format(logMessage, lead.Id, lead.LastName, lead.FirstName, lead.Patronymic, lead.Email);
                Log.Information(logMessage);
                //send email about status change
            }
        }

        public bool CheckOneLead(LeadOutputModel lead)
        {
            return (
                CheckBirthdayCondition(lead)
                //||CheckOperationsCondition(lead) 
                //||CheckBalanceCondition(lead)
                );
        }


        public bool CheckOperationsCondition(LeadOutputModel lead)
        {
            int transactionsCount = 0;
            foreach (var account in lead.Accounts)
            {
                TimeBasedAcquisitionInputModel period = new TimeBasedAcquisitionInputModel
                {
                    To = DateTime.Now.ToString(_dateFormatWithMinutesAndSeconds),
                    From = DateTime.Now.AddDays(-Const.PERIOD_FOR_CHECK_TRANSACTIONS_FOR_VIP).ToString(_dateFormatWithMinutesAndSeconds),
                    AccountId = account.Id
                };

                var transactions = _requests.GetTransactionsByPeriod(period, _adminToken).FirstOrDefault();

                if (transactions.Transactions != null && transactions.Transactions.Count > 0)
                {
                    transactionsCount += transactions.Transactions.
                    Where(t => t.TransactionType == TransactionType.Deposit).Count();
                }
                if(transactions.Transfers != null && transactions.Transfers.Count > 0)
                {
                    transactionsCount += transactions.Transfers.Count();
                }
                
                if (transactionsCount > Const.COUNT_TRANSACTIONS_IN_PERIOD_FOR_VIP) return true;
            }
            return false;
        }

        public bool CheckBalanceCondition(LeadOutputModel lead)
        {
            decimal sumDeposit = 0;
            decimal sumWithdraw = 0;
            TimeBasedAcquisitionInputModel model = new TimeBasedAcquisitionInputModel
            {
                To = DateTime.Now.ToString(),
                From = DateTime.Now.AddDays(-Const.PERIOD_FOR_CHECK_SUM_FOR_VIP).ToString(),
                AccountId = lead.Id
            };
            var transactions = _requests.GetTransactionsByPeriod(model, _adminToken);

            foreach (var accountBusinessModel in transactions)
            {
                foreach (var transaction in accountBusinessModel.Transactions)
                {
                    if (transaction.TransactionType == TransactionType.Deposit)
                    {
                        if (transaction.Currency == Currency.RUB)
                        {
                            sumDeposit += transaction.Amount;
                        }
                        else
                        {
                            //var money = transion.Amoun
                        }

                    }

                    if (transaction.TransactionType == TransactionType.Withdraw)
                    {
                        if (transaction.Currency == Currency.RUB)
                        {
                            sumWithdraw += transaction.Amount;
                        }
                        else
                        {

                        }
                    }
                }

            }

            return (Math.Abs(sumWithdraw) > sumDeposit + Const.SUM_DIFFERENCE_DEPOSIT_AND_WITHRAW_FOR_VIP);
        }

        public bool CheckBirthdayCondition(LeadOutputModel lead)
        {
            var leadBirthDate = Convert.ToDateTime(lead.BirthDate);

            if (leadBirthDate <= DateTime.Today
                && leadBirthDate >= DateTime.Today.AddDays(-Const.COUNT_DAY_AFTER_BDAY_FOR_VIP))
            {
                if (leadBirthDate.Day == DateTime.Now.Day
                && leadBirthDate.Month == DateTime.Now.Month)
                {
                    //send email
                    return true;
                }
                return true;
            }
            return false;
        }

    }
}
