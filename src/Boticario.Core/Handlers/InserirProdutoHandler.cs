﻿using Boticario.Core.Domains;
using Boticario.Core.Helpers;
using Boticario.Core.Interfaces.Repositories;
using Boticario.Core.Interfaces.UoW;
using Boticario.Core.Model.Commands.Base;
using Boticario.Core.Model.Commands.Produto;
using Boticario.Core.ValidationQueries;
using Flunt.Validations;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Boticario.Core.Handlers.Produto
{
    public class InserirProdutoHandler : CommandHandler<InserirProdutoCommand, CommandResult>
    {
        private readonly IProdutoRepository _produtoRepository;

        public InserirProdutoHandler(IUnitOfWork unitOfWork, IProdutoRepository produtoRepository) 
            : base(unitOfWork)
        {
            _produtoRepository = produtoRepository;
        }

        public override async Task<CommandResult> Handle(InserirProdutoCommand command, CancellationToken cancellationToken)
        {
            await Validate(command);

            if (Invalid)
                return new CommandResult(false, erros: ValidationErrors);

            var produto = new Domains.Produto(
                command.Sku,
                command.Name,
                command.Inventory.Warehouses.Select(
                    x => new Estoque
                    (
                        x.Locality,
                        x.Quantity.Value,
                        x.Type.ToUpper()
                    )).ToList()
            ); 

            await _produtoRepository.Inserir(produto);
            await Commit();

            return new CommandResult(true);
        }

        private async Task Validate(InserirProdutoCommand command)
        {
            var skuJaExiste = await _produtoRepository.Existe(ProdutoValidationQuery.GetProdutoComMesmoSKU(command.Sku));

            AddNotifications(new Contract()
                .Requires()
                .IsGreaterThan(command.Sku, 0, nameof(command.Sku), "O Sku é obrigatório.")
                .IsNotNullOrWhiteSpace(command.Name, nameof(command.Name), "O nome é obrigatório.")
                .IsFalse(skuJaExiste, nameof(command.Sku), "Já existe um SKU cadastrado com esse valor.")
                .HasMinLen(command.Name, 3, nameof(command.Name), "O nome precisa ter no mínimo 3 caracteres.")
                .HasMaxLen(command.Name, 100, nameof(command.Name), "O nome deve ter no máximo 100 caracteres.")
            );

            if(command.Inventory != null)
            {
                    if (command.Inventory.Warehouses.Count > 0)
                {
                    foreach (var estoque in command.Inventory.Warehouses)
                    {
                        if (EstoqueHelper.ValidaTipo(estoque.Type.ToUpper()) == false)
                        {
                            AddNotification(nameof(estoque.Type), "Tipo de estoque inválido.");
                        }

                        AddNotifications(new Contract()
                           .Requires()
                           .IsNotNullOrWhiteSpace(estoque.Locality, nameof(estoque.Locality), "O local é obrigatório.")
                           .HasMinLen(estoque.Locality, 2, nameof(estoque.Locality), "O local precisa ter no mínimo 3 caracteres.")
                           .HasMaxLen(estoque.Locality, 20, nameof(estoque.Locality), "O local deve ter no máximo 100 caracteres.")
                           .IsNotNull(estoque.Quantity, nameof(estoque.Quantity), "A quantidade é obrigatório.")
                           .IsNotNullOrWhiteSpace(estoque.Type, nameof(estoque.Type), "O tipo é obrigatório.")
                        );
                    }
                }
            }
        }
    }
}