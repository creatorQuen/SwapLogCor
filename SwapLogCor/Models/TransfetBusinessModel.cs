﻿using SwapLogCor.Enums;

namespace SwapLogCor.Models
{
    public class TransferBusinessModel : TransactionBusinessModel
    {
        public long RecipientTransactionId { get; set; }
        public int RecipientAccountId { get; set; }
        public decimal RecipientAmount { get; set; }
        public Currency RecipientCurrency { get; set; }
    }
}
