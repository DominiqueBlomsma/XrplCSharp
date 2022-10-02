﻿using System;
using System.Collections.Generic;
using System.Transactions;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.Utilities.Encoders;
using Xrpl.AddressCodecLib;
using Xrpl.BinaryCodecLib;
using Xrpl.BinaryCodecLib.Enums;
using Xrpl.BinaryCodecLib.Transactions;
using Xrpl.BinaryCodecLib.Types;
using Xrpl.BinaryCodecLib.Util;
using Xrpl.KeypairsLib;
using Xrpl.ClientLib.Exceptions;
using Xrpl.Models.Transactions;
using Org.BouncyCastle.Math;
using System.Diagnostics;
using static Xrpl.AddressCodecLib.B58;
using IKeypairs = Xrpl.KeypairsLib.Keypairs;


// https://github.com/XRPLF/xrpl.js/blob/main/packages/xrpl/src/Wallet/signer.ts

namespace Xrpl.WalletLib
{
    public class Signer
    {

        /// <summary>
        /// Takes several transactions with Signer fields (in object or blob form) and creates a single transaction with all Signers that then gets signed and returned.
        /// </summary>
        /// <param name="txs">An array of signed Transactions (in object or blob form) to combine into a single signed Transaction.</param>
        /// <returns>A single signed Transaction which has all Signers from transactions within it.</returns>
        /// <throws>        * @throws ValidationError; There were no transactions given to sign. The SigningPubKey field is not the empty string in any given transaction. Any transaction is missing a Signers field.</throws>
        public static string Multisign(Dictionary<string, dynamic>[] txs)
        {
            if (txs.Length == 0)
            {
                throw new ValidationError("There were 0 transactions to multisign");
            }
            foreach (Dictionary<string, dynamic> i in txs)
            {
                Dictionary<string, dynamic> tx = GetDecodedTransaction(i);
                //TxFormat.Validate(tx);
                if (tx["Signers"] == null || tx["Signers"].Length == 0)
                {
                    throw new ValidationError("For multisigning all transactions must include a Signers field containing an array of signatures. You may have forgotten to pass the 'forMultisign' parameter when signing.");
                }
                if (tx["SigningPubKey"] != "")
                {
                    throw new ValidationError("SigningPubKey must be an empty string for all transactions when multisigning.");
                }
            }

            Dictionary<string, dynamic>[] decodedTransactions = { };
            //Dictionary<string, dynamic>[] decodedTransactions = transactions.map(
            //    (txOrBlob: string | Transaction) => {
            //        return GetDecodedTransaction(txOrBlob)
            //    },
            //  )
            ValidateTransactionEquivalence(decodedTransactions);
            return BinaryCodec.Encode(GetTransactionWithAllSigners(decodedTransactions));
        }

        /// <summary>
        /// Creates a signature that can be used to redeem a specific amount of XRP from a payment channel.
        /// </summary>
        /// <param name="wallet">The account that will sign for this payment channel.</param>
        /// <param name="channelID">An id for the payment channel to redeem XRP from.</param>
        /// <param name="amount">The amount in drops to redeem.</param>
        /// <returns>A signature that can be used to redeem a specific amount of XRP from a payment channel.</returns>
        public static string AuthorizeChannel(Wallet wallet, string channelID, string amount)
        {
            Dictionary<string, dynamic> json = new Dictionary<string, dynamic>();
            json.Add("channel", channelID);
            json.Add("amount", amount);
            Debug.WriteLine(json);
            string signatureData = BinaryCodec.EncodeForSigningClaim(json);
            Debug.WriteLine(signatureData);
            return IKeypairs.Sign(signatureData.FromHex(), wallet.PrivateKey);
        }

        /// <summary>
        /// Verifies that the given transaction has a valid signature based on public-key encryption.
        /// </summary>
        /// <param name="tx">A transaction object to verify the signature of. (Can be in object or encoded string format).</param>
        /// <returns>Returns true if tx has a valid signature, and returns false otherwise.</returns>
        public static bool VerifySignature(Dictionary<string, dynamic> tx)
        {
            Dictionary<string, dynamic> decodedTx = GetDecodedTransaction(tx);
            Debug.WriteLine(BinaryCodec.EncodeForSigning(decodedTx).FromHex().ToHex());
            Debug.WriteLine((string)decodedTx["TxnSignature"]);
            Debug.WriteLine((string)decodedTx["SigningPubKey"]);
            return IKeypairs.Verify(
              BinaryCodec.EncodeForSigning(decodedTx).FromHex(),
              decodedTx["TxnSignature"],
              decodedTx["SigningPubKey"]
            );
        }

        /// <summary>
        /// Verifies that the given transaction has a valid signature based on public-key encryption.
        /// </summary>
        /// <param name="tx">A transaction string to verify the signature of.</param>
        /// <returns>Returns true if tx has a valid signature, and returns false otherwise.</returns>
        public static bool VerifySignature(string tx)
        {
            Dictionary<string, dynamic> decodedTx = GetDecodedTransaction(tx);
            return IKeypairs.Verify(
              BinaryCodec.EncodeForSigning(decodedTx).FromHex(),
              decodedTx["TxnSignature"],
              decodedTx["SigningPubKey"]
            );
        }

        /// <summary>
        /// The transactions should all be equal except for the 'Signers' field.
        /// </summary>
        /// <param name="transactions">An array of Transactions which are expected to be equal other than 'Signers'.</param>
        /// <returns>Returns true if tx has a valid signature, and returns false otherwise.</returns>
        /// <throws>ValidationError if the transactions are not equal in any field other than 'Signers'.</throws>
        public static void ValidateTransactionEquivalence(Dictionary<string, dynamic>[] transactions)
        {
            Dictionary<string, dynamic>  exampleTx = transactions[0];
            exampleTx["Signers"] = null;
            string exampleTransaction = exampleTx.ToString();
            //if (transactions.Slice(1).Some(tx) != exampleTransaction)
            //{
            //    throw new ValidationError("txJSON is not the same for all signedTransactions");
            //}
        }

        public static Dictionary<string, dynamic> GetTransactionWithAllSigners(Dictionary<string, dynamic>[] transactions)
        {
            // Signers must be sorted in the combined transaction - See compareSigners' documentation for more details
            Dictionary<string, dynamic> finalTx = transactions[0];
            //transactions
            //finalTx["Signers"] = sortedSigners;
            return finalTx;
        }

        /// <summary>
        /// If presented in binary form, the Signers array must be sorted based on
        /// the numeric value of the signer addresses, with the lowest value first.
        /// (If submitted as JSON, the submit_multisigned method handles this automatically.)
        /// https://xrpl.org/multi-signing.html.
        /// </summary>
        /// <param name="left">A Signer to compare with.</param>
        /// <param name="right">A Signer to compare with.</param>
        /// <returns>Returns 1 if left > right, 0 if left = right, -1 if left < right, and null if left or right are NaN.</returns>
        public static int CompareSigners(Dictionary<string, dynamic> left, Dictionary<string, dynamic> right)
        {
            return AddressToBigNumber(left["Signer"]["Account"]) == AddressToBigNumber(right["Signer"]["Account"]);
        }

        public static BigInteger AddressToBigNumber(string address)
        {
            string hex = XrplCodec.DecodeAccountID(address).ToHex();
            return new BigInteger(hex, 16);
        }

        public static Dictionary<string, dynamic> GetDecodedTransaction(Dictionary<string, dynamic>  txOrBlob)
        {
            return BinaryCodec.Decode(BinaryCodec.Encode(txOrBlob)).ToObject<Dictionary<string, dynamic>>();
        }

        public static Dictionary<string, dynamic> GetDecodedTransaction(string txOrBlob)
        {
            return BinaryCodec.Decode(txOrBlob).ToObject<Dictionary<string, dynamic>>();
        }
    }
}