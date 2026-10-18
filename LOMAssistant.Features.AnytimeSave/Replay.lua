-- Local aliases: each loaded story closure owns its wrappers.
local function ams_query(name, fn, ...)
    return __ams_query(name, fn, ...)
end
local function ams_say(text, voice)
    local skip = __ams_begin('say', tostring(text))
    if not skip then say(text, voice) end
    __ams_end(nil)
end
local function ams_choose(options)
    local skip = __ams_begin('choose', table.concat(options, '|'))
    local result
    if skip then result = __ams_reply() else result = choose(options) end
    __ams_end(result)
    return result
end
local function ams_choosetimer(options, duration, defaultoption)
    local skip = __ams_begin('timer', table.concat(options, '|'))
    local result
    if skip then result = __ams_reply() else result = choosetimer(options, duration, defaultoption) end
    __ams_end(result)
    return result
end
local function ams_roll(enumerator)
    local skip = __ams_begin('roll', 'ExecuteRoll')
    if not skip then runwait(enumerator) end
    __ams_end(nil)
end
local function ams_panel(enumerator)
    -- Panels such as shops require user input. Their completed mutations are
    -- already in the saved snapshot; queries after the panel are in the trace.
    if not __ams_replaying() then
        __ams_busy(true)
        runwait(enumerator)
        __ams_busy(false)
    end
end
local function ams_wait(duration)
    if not __ams_replaying() then wait(duration) end
end

