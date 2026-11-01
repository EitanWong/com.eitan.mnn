using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MNN.Unity
{
    public interface IMNNSecretStore
    {
        string Protection { get; }

        bool CanPersist { get; }

        string Read(string alias);
        void Write(string alias, string secret);
        void Delete(string alias);
    }
}
