namespace CarForge.Core.Editing;

/// <summary>O que cada campo do handling.meta faz, em português direto.</summary>
public static class HandlingFieldDocs
{
    private static readonly Dictionary<string, string> Docs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fMass"] = "Massa do veículo (kg). Mais pesado = mais difícil de empurrar/parar e mais estável.",
        ["fInitialDragCoeff"] = "Arrasto do ar. Maior = perde velocidade mais rápido em alta.",
        ["fPercentSubmerged"] = "Quanto afunda na água antes de boiar/afogar o motor.",
        ["fDriveBiasFront"] = "Distribuição de tração: 0 = traseira (RWD), 1 = dianteira (FWD), 0.5 = 4x4.",
        ["nInitialDriveGears"] = "Número de marchas da caixa.",
        ["fInitialDriveForce"] = "Força do motor. O principal da aceleração — quanto 'empurra'.",
        ["fDriveInertia"] = "Rapidez com que o motor ganha giro. Maior = sobe RPM mais rápido.",
        ["fClutchChangeRateScaleUpShift"] = "Velocidade de troca de marcha subindo (acelerando).",
        ["fClutchChangeRateScaleDownShift"] = "Velocidade de troca de marcha descendo (reduzindo).",
        ["fInitialDriveMaxFlatVel"] = "Velocidade máxima aproximada em terreno plano (km/h).",
        ["fBrakeForce"] = "Força do freio. Maior = freia mais forte.",
        ["fBrakeBiasFront"] = "Distribuição da frenagem: 1 = só dianteira, 0 = só traseira, 0.5 = equilibrado.",
        ["fHandBrakeForce"] = "Força do freio de mão (importante pra drift/derrapagem).",
        ["fSteeringLock"] = "Ângulo máximo de esterçamento. Maior = roda vira mais (mais 'fechado').",
        ["fTractionCurveMax"] = "Aderência máxima (grip). Maior = mais grude na curva, menos desliza.",
        ["fTractionCurveMin"] = "Aderência quando já está deslizando. Define o comportamento no limite.",
        ["fTractionCurveLateral"] = "Aderência lateral (resistência a escorregar de lado).",
        ["fTractionSpringDeltaMax"] = "Quanto o pneu pode 'soltar' do chão antes de perder tração.",
        ["fLowSpeedTractionLossMult"] = "Perda de tração em baixa velocidade (arrancada cantando pneu).",
        ["fCamberStiffness"] = "Rigidez de câmber do pneu (afeta grip em curva).",
        ["fTractionBiasFront"] = "Distribuição de aderência frente/trás. >0.5 puxa pra sobre-esterço (rabeia).",
        ["fTractionLossMult"] = "Multiplicador de perda de tração em superfícies ruins (terra, molhado).",
        ["fSuspensionForce"] = "Força da suspensão. Maior = mais firme, segura melhor o peso.",
        ["fSuspensionCompDamp"] = "Amortecimento na compressão (ao passar em buraco/quebra-mola).",
        ["fSuspensionReboundDamp"] = "Amortecimento no retorno da suspensão (volta após comprimir).",
        ["fSuspensionUpperLimit"] = "Limite superior do curso da suspensão.",
        ["fSuspensionLowerLimit"] = "Limite inferior do curso da suspensão.",
        ["fSuspensionRaise"] = "Altura da suspensão. Positivo levanta, negativo abaixa o carro.",
        ["fSuspensionBiasFront"] = "Distribuição da rigidez da suspensão frente/trás.",
        ["fAntiRollBarForce"] = "Barra estabilizadora. Maior = menos rolagem (carroceria balança menos na curva).",
        ["fAntiRollBarBiasFront"] = "Distribuição da barra estabilizadora frente/trás.",
        ["fRollCentreHeightFront"] = "Centro de rolagem dianteiro (estabilidade na curva).",
        ["fRollCentreHeightRear"] = "Centro de rolagem traseiro.",
        ["fCollisionDamageMult"] = "Multiplicador de dano por colisão na lataria.",
        ["fWeaponDamageMult"] = "Multiplicador de dano por armas.",
        ["fDeformationDamageMult"] = "O quanto a lataria deforma ao bater.",
        ["fEngineDamageMult"] = "O quanto o motor se danifica.",
        ["fPetrolTankVolume"] = "Volume do tanque (afeta explosão/incêndio).",
        ["fOilVolume"] = "Volume de óleo.",
        ["fSeatOffsetDistX"] = "Ajuste da posição do motorista no eixo X.",
        ["fSeatOffsetDistY"] = "Ajuste da posição do motorista no eixo Y.",
        ["fSeatOffsetDistZ"] = "Ajuste da posição do motorista no eixo Z (altura).",
        ["nMonetaryValue"] = "Valor monetário de referência do veículo.",
        ["fCentreOfMassOffsetX"] = "Desloca o centro de massa lateralmente.",
        ["fCentreOfMassOffsetY"] = "Desloca o centro de massa frente/trás.",
        ["fCentreOfMassOffsetZ"] = "Desloca o centro de massa em altura (baixo = mais estável).",
        ["fInitialDragCoeff "] = "Arrasto do ar (variação com espaço no nome).",
    };

    /// <summary>Descrição em PT do campo, ou string vazia se não catalogado.</summary>
    public static string Describe(string field) =>
        Docs.TryGetValue(field, out var d) ? d : "Campo de handling (sem descrição catalogada).";
}
