/* ========================================================================
 * Copyright (c) 2005-2020 The OPC Foundation, Inc. All rights reserved.
 *
 * OPC Foundation MIT License 1.00
 * 
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following
 * conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 *
 * The complete license agreement can be found here:
 * http://opcfoundation.org/License/MIT/1.00/
 * ======================================================================*/

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Opc.Ua.Client
{
    /// <summary>
    /// A client session with a server.
    /// </summary>
    public interface ISession
    {
        /// <summary>
        /// Gets the factory used to create encodeable objects that the server understands.
        /// </summary>
        EncodeableFactory Factory { get; }

        /// <summary>
        /// Gets the table of namespace uris known to the server.
        /// </summary>
        NamespaceTable NamespaceUris { get; }

        /// <summary>
        /// Gest the table of remote server uris known to the server.
        /// </summary>
        StringTable ServerUris { get; }

        /// <summary>
        /// Gets the cache of nodes fetched from the server.
        /// </summary>
        NodeCache NodeCache { get; }

        /// <summary>
        /// Loads all dictionaries of the OPC binary or Xml schema type system.
        /// </summary>
        /// <param name="dataTypeSystem">The type system.</param>
        /// <returns></returns>
        Task<Dictionary<NodeId, DataDictionary>> LoadDataTypeSystem(NodeId dataTypeSystem = null);

        /// <summary>
        /// Fetches all references for the specified node.
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <returns></returns>
        ReferenceDescriptionCollection FetchReferences(NodeId nodeId);

        /// <summary>
        /// Reads the values for the node attributes and returns a node object.
        /// </summary>
        /// <param name="nodeId">The nodeId.</param>
        /// <returns></returns>
        Node ReadNode(NodeId nodeId);

        /// <summary>
        /// Reads the value for a node.
        /// </summary>
        /// <param name="nodeId">The node Id.</param>
        /// <returns></returns>
        DataValue ReadValue(NodeId nodeId);
    }
}//namespace
